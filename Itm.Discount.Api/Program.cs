using Itm.Discount.Api.Dtos;

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

// Base de datos simulada
var discountsDb = new List<DiscountDto>
{
    new("ITM50", 0.5m)
};

app.MapGet("/api/discounts/{code}", (string code) =>
{
    var discount = discountsDb.FirstOrDefault(d =>
        d.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase));

    return discount is not null ? Results.Ok(discount) : Results.NotFound();
});

app.Run();


