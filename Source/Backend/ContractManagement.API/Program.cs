using ContractManagement.API.Domains.Interfaces.Catalog;
using ContractManagement.API.Domains.Interfaces.Customer;
using ContractManagement.API.Domains.Interfaces.CustomerInteraction;
using ContractManagement.API.Domains.Interfaces.Department;
using ContractManagement.API.Domains.Interfaces.Authentication;
using ContractManagement.API.Domains.Interfaces.Admin;
using ContractManagement.API.Domains.Interfaces.Dashboard;
using ContractManagement.API.Domains.Interfaces.SystemAuthentication;
using ContractManagement.API.Domains.Interfaces.Security;
using ContractManagement.API.Domains.Interfaces.LegalProfiles;
using ContractManagement.API.Domains.Services.Catalog;
using ContractManagement.API.Domains.Services.Customer;
using ContractManagement.API.Domains.Services.CustomerInteraction;
using ContractManagement.API.Domains.Services.Department;
using ContractManagement.API.Domains.Services.Authentication;
using ContractManagement.API.Domains.Services.Admin;
using ContractManagement.API.Domains.Services.Dashboard;
using ContractManagement.API.Domains.Services.SystemAuthentication;
using ContractManagement.API.Domains.Services.Employee;
using ContractManagement.API.Domains.Services.Security;
using ContractManagement.API.Domains.Services.LegalProfiles;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.Employee;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Interfaces.Quotation;
using ContractManagement.Domains.Mappings.Quotation;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.File;
using ContractManagement.Domains.Services.Quotation;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.DatabaseScripts.SeedData;
using ContractManagement.Infrastructure.MultiTenancy.DI;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Middleware;
using ContractManagement.Middleware.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using ContractManagement.API.Helpers;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.API.Domains.Discussions;
using System.Net;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Recovery;

var builder = WebApplication.CreateBuilder(args);

ValidateProductionConfiguration(builder.Configuration, builder.Environment);

// Chỉ tin X-Forwarded-* do reverse proxy trên cùng máy gửi tới Kestrel.
// Điều này giúp HTTPS redirect, HSTS và URL trong response phản ánh đúng
// giao thức của client khi Nginx kết thúc TLS phía trước API.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];
if (builder.Environment.IsDevelopment() && allowedCorsOrigins.Length == 0)
{
    allowedCorsOrigins =
    [
        "http://localhost:3000",
        "http://localhost:3001",
        "http://localhost:8081"
    ];
}

#region 1. MVC Controllers

/*
 * Đăng ký Controller cho Web API.
 */
builder.Services.AddControllers();
builder.Services.AddScoped<ContractManagement.Domains.Services.Catalog.CatalogWorkspaceService>();
builder.Services.AddSignalR();
builder.Services.AddOptions<ContractManagement.API.Domains.Realtime.NodeRealtimeOptions>()
    .Bind(builder.Configuration.GetSection(ContractManagement.API.Domains.Realtime.NodeRealtimeOptions.Section))
    .Validate(options => options.IsValid(), "Node realtime requires loopback URLs and a service secret of at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddHttpClient("RealtimeInternal", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false });
builder.Services.AddSingleton<ContractManagement.API.Domains.Realtime.RealtimeServiceAuthentication>();
builder.Services.AddSingleton<ContractManagement.API.Domains.Realtime.NodeRealtimeBroker>();
builder.Services.AddSingleton<ContractManagement.API.Domains.Realtime.NodeRealtimePublisher>();
builder.Services.AddHostedService<ContractManagement.API.Domains.Realtime.NodeNotificationDispatchWorker>();
builder.Services.AddScoped<ContractDiscussionAccessService>();
builder.Services.AddScoped<IContractDiscussionRealtimePublisher, ContractDiscussionRealtimePublisher>();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
#endregion

#region 2. Swagger / OpenAPI

/*
 * Dùng để hiển thị Swagger UI và tài liệu API.
 */
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFileName =
        $"{builder.Environment.ApplicationName}.xml";

    var xmlFilePath = Path.Combine(
        AppContext.BaseDirectory,
        xmlFileName);

    options.IncludeXmlComments(xmlFilePath);
});

#endregion

#region 3. Multi-tenancy Infrastructure

/*
 * Đăng ký toàn bộ thành phần Infrastructure:
 *
 * - CentralDbContext
 * - DbDtctechContext dùng connection string động
 * - CurrentTenant
 * - TenantResolver
 * - TenantProvisioningService
 * - TenantDatabaseInitializer
 *
 * Không đăng ký DbDtctechContext thêm lần nữa trong Program.cs.
 */
builder.Services.AddContractManagementInfrastructure(
    builder.Configuration);

builder.Services.AddOptions<SmtpEmailOptions>()
    .Bind(builder.Configuration.GetSection(SmtpEmailOptions.SectionName))
    .Validate(options => (!string.Equals(builder.Configuration["CustomerOtp:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase)
        && !(builder.Configuration.GetValue<bool>("AccountRecovery:Enabled") && builder.Configuration["AccountRecovery:Provider"] != "Test"))
        || options.IsConfigured(), "Email:Smtp requires valid STARTTLS credentials, sender and timeout.")
    .ValidateOnStart();
builder.Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<AccountRecoveryOptions>()
    .Bind(builder.Configuration.GetSection(AccountRecoveryOptions.SectionName))
    .Validate(options => options.IsValid(builder.Environment.IsDevelopment()),
        "AccountRecovery requires a persistent 32-byte encryption key, trusted frontend URLs and valid limits; Test is Development only.")
    .ValidateOnStart();
builder.Services.AddSingleton<AccountRecoveryCryptography>();
builder.Services.AddSingleton<RecoveryTestMailbox>();
builder.Services.AddScoped<RecoveryEmailVerificationService>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.AddSingleton<RecoveryRequestLimiter>();
builder.Services.AddSingleton<RecoveryTelemetry>();
builder.Services.AddHostedService<AccountRecoveryOutboxWorker>();

builder.Services.AddOptions<CustomerOtpOptions>()
    .Bind(builder.Configuration.GetSection(CustomerOtpOptions.SectionName))
    .Validate(options => builder.Environment.IsDevelopment()
        || (IsThirtyTwoByteBase64(options.HashKey)
            && IsThirtyTwoByteBase64(options.EncryptionKey)),
        "Production customer OTP requires configured 32-byte hash/encryption keys.")
    .Validate(options => options.UsesSmtp
        ? builder.Configuration.GetSection(SmtpEmailOptions.SectionName).Get<SmtpEmailOptions>()?.IsConfigured() == true
        : string.Equals(options.Provider, "Fake", StringComparison.OrdinalIgnoreCase)
            ? builder.Environment.IsDevelopment()
            : string.Equals(options.Provider, "Http", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(options.ProviderEndpoint)
                && !string.IsNullOrWhiteSpace(options.ProviderApiKey),
        "CustomerOtp:Provider must be Fake (Development only), Http (endpoint/API key required), or Smtp (host, port 587, username, app password, from address and timeout 1-45 seconds required).")
    .ValidateOnStart();
builder.Services.AddSingleton<CustomerAccessCryptography>();

var otpProvider = builder.Configuration["CustomerOtp:Provider"] ?? "Fake";
if (string.Equals(otpProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICustomerOtpDeliveryProvider, SmtpCustomerOtpDeliveryProvider>();
}
else if (string.Equals(otpProvider, "Fake", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICustomerOtpDeliveryProvider, FakeCustomerOtpDeliveryProvider>();
}
else
{
    builder.Services.AddHttpClient<ICustomerOtpDeliveryProvider, HttpCustomerOtpDeliveryProvider>();
}
builder.Services.AddHostedService<CustomerOtpDeliveryOutboxWorker>();
builder.Services.AddHostedService<NotificationDispatchWorker>();
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<INotificationResourcePolicy, ContractNotificationResourcePolicy>();
builder.Services.AddScoped<ContractNotificationProducer>();

#endregion

#region 4. Session

/*
 * Session hiện được lưu trong bộ nhớ của server.
 *
 * Phù hợp khi phát triển hoặc chỉ chạy một server.
 * Khi scale nhiều server, nên dùng Redis hoặc SQL Server Session.
 */
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    /*
     * Session hết hạn sau 30 phút không hoạt động.
     */
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    /*
     * JavaScript phía frontend không đọc được session cookie.
     * Giúp hạn chế nguy cơ đánh cắp cookie bằng XSS.
     */
    options.Cookie.HttpOnly = true;

    /*
     * Cho phép cookie session hoạt động
     * dù người dùng chưa đồng ý cookie không thiết yếu.
     */
    options.Cookie.IsEssential = true;

    options.Cookie.Name = "ContractManagement.Session";

    /*
     * Cho phép cookie được gửi trong các request thông thường
     * nhưng hạn chế một số request cross-site.
     */
    options.Cookie.SameSite = SameSiteMode.Lax;

    /*
     * Local HTTP vẫn chạy được.
     * Khi request dùng HTTPS thì cookie cũng dùng Secure.
     */
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});

#endregion

#region 5. CORS

/*
 * Cho phép React frontend gọi API và gửi session cookie.
 */
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion

#region 6. Authentication helpers

/*
 * Service dùng để hash và kiểm tra mật khẩu nhân viên.
 */
builder.Services.AddScoped<
    IPasswordHasher<TblEmployee>,
    PasswordHasher<TblEmployee>>();

/*
 * Service dùng để hash và kiểm tra mật khẩu SystemAdmin.
 */
builder.Services.AddScoped<
    IPasswordHasher<SystemAdmin>,
    PasswordHasher<SystemAdmin>>();
#endregion

#region 7. Business services

/*
 * Đăng ký các service nghiệp vụ.
 */
builder.Services.AddScoped<
    IQuotationService,
    QuotationService>();

builder.Services.AddScoped<
    IFileStorageService,
    FileStorageService>();

builder.Services.AddOptions<PrivateFileStorageOptions>()
    .Bind(builder.Configuration.GetSection(PrivateFileStorageOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.RootPath),
        "PrivateFileStorage:RootPath is required.")
    .Validate(
        options => options.MinimumFreeSpaceBytes >= 0,
        "PrivateFileStorage:MinimumFreeSpaceBytes cannot be negative.")
    .ValidateOnStart();
builder.Services.AddSingleton<LocalPrivateFileStorage>();
builder.Services.AddSingleton<IPrivateFileStorage>(provider =>
    provider.GetRequiredService<LocalPrivateFileStorage>());
builder.Services.AddSingleton<IPrivateFileStorageHealthProbe>(provider =>
    provider.GetRequiredService<LocalPrivateFileStorage>());

builder.Services.AddScoped<
    IContractResourceAuthorizationService,
    ContractResourceAuthorizationService>();

builder.Services.AddScoped<
    IFileResourceAuthorizationService,
    FileResourceAuthorizationService>();

builder.Services.AddScoped<
    ITenantAuthorizationAuditWriter,
    TenantAuthorizationAuditWriter>();

builder.Services.AddScoped<
    ICentralSecurityAuditWriter,
    CentralSecurityAuditWriter>();

builder.Services.AddScoped<
    ITenantSecurityAuditQueryService,
    TenantSecurityAuditQueryService>();

builder.Services.AddScoped<
    ICentralSecurityAuditQueryService,
    CentralSecurityAuditQueryService>();

builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();

builder.Services.AddScoped<
    IContractAttachmentService,
    ContractAttachmentService>();

builder.Services.AddScoped<
    IDepartmentService,
    DepartmentService>();

builder.Services.AddScoped<
    IEmployeeService,
    EmployeeService>();

builder.Services.AddScoped<
    IEmployeeAccountService,
    EmployeeAccountService>();

builder.Services.AddScoped<
    ISystemAdminAccountService,
    SystemAdminAccountService>();

builder.Services.AddScoped<
    ISystemAdminManagerGovernanceService,
    SystemAdminManagerGovernanceService>();

builder.Services.AddScoped<
    ICustomerService,
    CustomerService>();

builder.Services.AddScoped<
    ICustomerInteractionService,
    CustomerInteractionService>();

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    IServiceTypeService,
    ServiceTypeService>();

builder.Services.AddScoped<
    IServiceService,
    ServiceService>();

builder.Services.AddScoped<
    IContractService,
    ContractService>();

builder.Services.AddScoped<
    IContractApprovalService,
    ContractApprovalService>();

builder.Services.AddScoped<
    IContractSigningService,
    ContractSigningService>();
builder.Services.AddScoped<
    IContractCompletionService,
    ContractCompletionService>();
builder.Services.AddScoped<IContractHandoverService, ContractHandoverService>();
builder.Services.AddScoped<
    IContractAcceptanceService,
    ContractAcceptanceService>();
builder.Services.AddSingleton<
    IContractPaymentDueDateService,
    ContractPaymentDueDateService>();

builder.Services.AddScoped<
    ICustomerContractAccessService,
    CustomerContractAccessService>();

builder.Services.AddScoped<
    IContractAuditWriter,
    ContractAuditWriter>();

builder.Services.AddScoped<
    IContractAuditQueryService,
    ContractAuditQueryService>();

builder.Services.Configure<ContractManagement.Domains.Policies.ContractTemplate.CustomContractPlaceholderOptions>(
    builder.Configuration.GetSection("CustomContractPlaceholders"));
builder.Services.AddSingleton<IContractPlaceholderSourceRegistry, ContractPlaceholderSourceRegistry>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, ContractPlaceholderSourceProvider>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, ContractVersionPlaceholderSourceProvider>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, CustomerPlaceholderSourceProvider>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, TenantLegalProfilePlaceholderSourceProvider>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, ContractOwnerPlaceholderSourceProvider>();
builder.Services.AddSingleton<IContractPlaceholderSourceProvider, DepartmentPlaceholderSourceProvider>();
builder.Services.AddScoped<ContractPlaceholderCatalog>();
builder.Services.AddScoped<IContractPlaceholderCatalog>(sp => sp.GetRequiredService<ContractPlaceholderCatalog>());
builder.Services.AddScoped<ContractPlaceholderDefinitionService>();
builder.Services.AddScoped<IContractPlaceholderValueService, ContractPlaceholderValueService>();
builder.Services.AddScoped<IContractTemplateDocumentValidator, ContractTemplateDocumentValidator>();

builder.Services.AddScoped<
    IContractTemplateAuditWriter,
    ContractTemplateAuditWriter>();

builder.Services.AddScoped<
    IContractTemplatePreviewRenderer,
    ContractTemplatePreviewRenderer>();

builder.Services.AddOptions<TemplatePdfRenderingOptions>()
    .Bind(builder.Configuration.GetSection(TemplatePdfRenderingOptions.SectionName))
    .Validate(options => options.TimeoutSeconds is > 0 and <= 60,
        "Template PDF conversion timeout must be between 1 and 60 seconds.")
    .Validate(options => options.MaxOutputBytes is > 0 and <= 25 * 1024 * 1024,
        "Template PDF output limit must be at most 25 MiB.")
    .ValidateOnStart();

builder.Services.AddSingleton<IContractTemplatePdfRenderer,
    LibreOfficeContractTemplatePdfRenderer>();

builder.Services.AddScoped<
    IContractTemplateService,
    ContractTemplateService>();

builder.Services.AddScoped<
    ITenantLegalProfileService,
    TenantLegalProfileService>();

builder.Services.AddScoped<ContractDocumentPreviewService>();
builder.Services.AddScoped<IContractDocumentPreviewService>(provider =>
    provider.GetRequiredService<ContractDocumentPreviewService>());
builder.Services.AddScoped<IContractSubmissionArtifactRenderer>(provider =>
    provider.GetRequiredService<ContractDocumentPreviewService>());

#endregion

#region 8. AutoMapper

/*
 * Đăng ký profile ánh xạ dữ liệu báo giá.
 */
builder.Services.AddAutoMapper(config =>
{
    config.AddProfile<QuotationMappingProfile>();
});

#endregion

var app = builder.Build();
_ = app.Services.GetRequiredService<IContractPlaceholderSourceRegistry>();

// Khởi tạo sớm để fail-fast khi đường dẫn, quyền ghi hoặc dung lượng private
// storage không đạt yêu cầu, thay vì đợi tới request upload đầu tiên.
_ = app.Services.GetRequiredService<IPrivateFileStorage>();

#region 8.5 Seed central Data

using (var scope = app.Services.CreateScope())
{
    var centralSeedData =
        scope.ServiceProvider
            .GetRequiredService<ICentralSeedData>();

    await centralSeedData.InitializeAsync();
}

#endregion

#region 9. Development tools

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

#endregion

#region 10. HTTP middleware pipeline

app.UseForwardedHeaders();

/*
 * Chuyển HTTP sang HTTPS.
 */
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

/*
 * Bắt lỗi toàn cục và trả về response thống nhất.
 */
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Preserve signed body bytes before MVC deserializes internal Node control requests.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/internal/realtime"))
        context.Request.EnableBuffering(bufferThreshold: 16384, bufferLimit: 16384);
    await next(context);
});

/*
 * Xác định endpoint hiện tại.
 *
 * Phải đứng trước TenantResolutionMiddleware để middleware
 * đọc được metadata như AllowWithoutTenantAttribute.
 */
app.UseRouting();

/*
 * CORS phải chạy trước Controller.
 */
app.UseCors("CorsPolicy");

/*
 * Session phải chạy trước TenantResolutionMiddleware,
 * vì middleware có thể lấy TenantCode từ session.
 */
app.UseSession();

/*
 * Khi triển khai authentication chuẩn của ASP.NET Core,
 * thêm middleware này trước TenantResolutionMiddleware:
 *
 * app.UseAuthentication();
 */

/*
 * Xác định tenant hiện tại:
 *
 * 1. Đọc TenantCode từ session, claim hoặc header.
 * 2. Tra cứu trong Central Database.
 * 3. Lưu tenant vào CurrentTenant.
 * 4. DbDtctechContext dùng connection string của tenant đó.
 */
app.UseMiddleware<TenantResolutionMiddleware>();

/*
 * Ghi audit best-effort cho denial từ action/controller của tenant. Đặt sau
 * tenant resolution để writer luôn có tenant scope chính xác.
 */
app.UseMiddleware<TenantDeniedAuthorizationAuditMiddleware>();

/*
 * Kiểm tra quyền truy cập.
 */
app.UseAuthorization();

/*
 * Ánh xạ endpoint Controller.
 */
app.MapControllers();
app.MapHub<NotificationHub>("/api/hubs/notifications")
    .WithMetadata(new ContractManagement.Filter.SessionAuthorizeAttribute());
app.MapHub<EmployeeContractDiscussionHub>("/api/contracts/{contractId:int}/versions/{versionId:int}/discussion-hub")
    .WithMetadata(new ContractManagement.Filter.SessionAuthorizeAttribute());
// Kept under the existing HttpOnly customer cookie path. No link/session secrets in the URL.
app.MapHub<CustomerContractDiscussionHub>("/public/contracts/{tenantCode}/discussion-hub")
    .WithMetadata(new ContractManagement.Attributes.PublicCustomerAccessAttribute());

/*
 * Chuyển các request không khớp Controller
 * sang Next.js.
 */
app.MapReverseProxy();

if (app.Environment.IsDevelopment())
{
    var nodeOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContractManagement.API.Domains.Realtime.NodeRealtimeOptions>>().Value;
    var launchNode = nodeOptions.Enabled
        && builder.Configuration.GetValue("Development:LaunchNodeBackend", true);
    FrontendLauncher.Start(
        launchFrontends: builder.Configuration.GetValue("Development:LaunchFrontends", true),
        launchNodeBackend: launchNode,
        nodePort: launchNode ? new Uri(nodeOptions.BaseUrl).Port : 4000);
}
#endregion

/*
 * Không chạy SeedData cũ tại startup ở thời điểm này.
 *
 * Lý do:
 * SeedData dùng DbDtctechContext, nhưng lúc startup chưa có
 * request và chưa xác định được tenant.
 *
 * Seed dữ liệu tenant nên được thực hiện:
 * - Ngay sau khi tạo database tenant; hoặc
 * - Bằng một TenantDatabaseInitializer riêng.
 */

app.Run();

static bool IsThirtyTwoByteBase64(string? value)
{
    try
    {
        return !string.IsNullOrWhiteSpace(value)
            && Convert.FromBase64String(value).Length == 32;
    }
    catch (FormatException)
    {
        return false;
    }
}

static void ValidateProductionConfiguration(
    IConfiguration configuration,
    IHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var requiredConnections = new[] { "CentralDatabase", "TenantDatabaseTemplate" };
    foreach (var name in requiredConnections)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{name} phải được cấp qua environment/secret store trong production.");
        }
    }

    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length == 0
        || origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins production phải chứa ít nhất một HTTPS origin cố định.");
    }

    var allowedHosts = configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts)
        || allowedHosts.Contains('*')
        || string.Equals(allowedHosts, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "AllowedHosts production phải là hostname triển khai cụ thể.");
    }
}
