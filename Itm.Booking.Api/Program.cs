// Itm.Booking.Api/Program.cs
using Itm.Booking.Api.Dtos;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configuración de HttpClientFactory con Resiliencia
builder.Services.AddHttpClient("EventClient", client =>
{
    // Reemplaza el puerto con el que asigne Visual Studio a Event.Api
    client.BaseAddress = new Uri("http://localhost:5082");
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("DiscountClient", client =>
{
    // Reemplaza el puerto con el que asigne Visual Studio a Discount.Api
    client.BaseAddress = new Uri("http://localhost:5150");
})
.AddStandardResilienceHandler();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/bookings", async (BookingRequest request, IHttpClientFactory factory) =>
{
    var eventClient = factory.CreateClient("EventClient");
    var discountClient = factory.CreateClient("DiscountClient");

    // 1. LECTURA EN PARALELO
    var eventTask = eventClient.GetAsync($"/api/events/{request.EventId}");
    var discountTask = string.IsNullOrWhiteSpace(request.DiscountCode)
        ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
        : discountClient.GetAsync($"/api/discounts/{request.DiscountCode}");

    await Task.WhenAll(eventTask, discountTask);

    var eventResponse = await eventTask;
    if (!eventResponse.IsSuccessStatusCode)
        return Results.BadRequest("El evento no existe o no está disponible.");

    var eventInfo = await eventResponse.Content.ReadFromJsonAsync<EventDto>();

    // Manejo del descuento (puede ser 404 si no existe)
    decimal porcentajeDescuento = 0m;
    var discountResponse = await discountTask;
    if (discountResponse.IsSuccessStatusCode)
    {
        var discountInfo = await discountResponse.Content.ReadFromJsonAsync<DiscountDto>();
        if (discountInfo != null) porcentajeDescuento = discountInfo.Porcentaje;
    }

    // MATEMÁTICAS: Calcular el total
    decimal subtotal = (eventInfo?.PrecioBase ?? 0) * request.Tickets;
    decimal descuentoAplicado = subtotal * porcentajeDescuento;
    decimal totalAPagar = subtotal - descuentoAplicado;

    // 2. ACCIÓN: RESERVAR SILLAS (Inicio de SAGA)
    var reserveResponse = await eventClient.PostAsJsonAsync("/api/events/reserve",
        new { EventId = request.EventId, Quantity = request.Tickets });

    if (!reserveResponse.IsSuccessStatusCode)
        return Results.BadRequest("No hay sillas suficientes.");

    try
    {
        // 3. SIMULACIÓN DE PAGO (Punto Crítico)
        bool paymentSuccess = new Random().Next(1, 10) > 5;
        if (!paymentSuccess) throw new Exception("Fondos insuficientes en la pasarela de pago.");

        return Results.Ok(new
        {
            Status = "Éxito",
            Message = "¡Disfruta el concierto ITM!",
            TotalPagado = totalAPagar,
            Descuento = descuentoAplicado
        });
    }
    catch (Exception ex)
    {
        // 4. COMPENSACIÓN (El Ctrl+Z)
        Console.WriteLine($"[SAGA] Error en pago: {ex.Message}. Liberando sillas...");

        await eventClient.PostAsJsonAsync("/api/events/release",
            new { EventId = request.EventId, Quantity = request.Tickets });

        return Results.Problem(detail: $"Tu pago fue rechazado ({ex.Message}). No te preocupes, no te cobramos y tus sillas fueron liberadas.");
    }
});

app.Run();