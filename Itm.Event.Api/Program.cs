using Itm.Event.Api.Dtos;
using Itm.Event.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

var eventsDb = new List<EventEntity>
{
    new EventEntity { Id = 1, Nombre = "Concierto ITM", PrecioBase = 50000, SillasDisponibles = 100 }
};

app.MapGet("/api/events/{id}", (int id) =>
{
    var ev = eventsDb.FirstOrDefault(e => e.Id == id);
    return ev is not null ? Results.Ok(ev) : Results.NotFound();
});

app.MapPost("/api/events/reserve", (ReserveRequest request) =>
{
    var ev = eventsDb.FirstOrDefault(e => e.Id == request.EventId);
    if (ev is null) return Results.NotFound("Evento no encontrado.");

    if (ev.SillasDisponibles < request.Quantity)
        return Results.BadRequest("No hay sillas suficientes.");

    ev.SillasDisponibles -= request.Quantity;
    return Results.Ok(new { Mensaje = "Sillas reservadas", SillasRestantes = ev.SillasDisponibles });
});

app.MapPost("/api/events/release", (ReserveRequest request) =>
{
    var ev = eventsDb.FirstOrDefault(e => e.Id == request.EventId);
    if (ev is null) return Results.NotFound("Evento no encontrado.");

    ev.SillasDisponibles += request.Quantity; // El "Ctrl+Z"
    return Results.Ok(new { Mensaje = "Sillas liberadas", SillasRestantes = ev.SillasDisponibles });
});

app.Run();