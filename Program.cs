using WatchTogether.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Добавляем MVC
builder.Services.AddControllersWithViews();

// Добавляем SignalR (только один раз!)
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Маршруты
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR хаб (только один раз!)
app.MapHub<VideoHub>("/videoHub");

app.Run();