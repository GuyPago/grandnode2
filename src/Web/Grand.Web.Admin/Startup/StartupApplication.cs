using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.Drivers.FileSystem.Extensions;
using Grand.Infrastructure;
using Grand.Web.Admin.Infrastructure;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Services;
using Grand.Web.Common.View;

namespace Grand.Web.Admin.Startup;

public class StartupApplication : IStartupApplication
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        #region elFinder

        services.AddElFinderAspNetCore().AddFileSystemDriver();

        #endregion

        services.AddScoped<IAddressAttributeViewModelService, AddressAttributeViewModelService>();
        services.AddScoped<IAffiliateViewModelService, AffiliateViewModelService>();
        services.AddScoped<IBlogViewModelService, BlogViewModelService>();
        services.AddScoped<ICampaignViewModelService, CampaignViewModelService>();
        services.AddScoped<ICategoryViewModelService, CategoryViewModelService>();
        services.AddScoped<ICheckoutAttributeViewModelService, CheckoutAttributeViewModelService>();
        services.AddScoped<IContactAttributeViewModelService, ContactAttributeViewModelService>();
        services.AddScoped<IContactFormViewModelService, ContactFormViewModelService>();
        services.AddScoped<ICountryViewModelService, CountryViewModelService>();
        services.AddScoped<ICourseViewModelService, CourseViewModelService>();
        services.AddScoped<ICurrencyViewModelService, CurrencyViewModelService>();
        services.AddScoped<ICustomerAttributeViewModelService, CustomerAttributeViewModelService>();
        services.AddScoped<ICustomerViewModelService, CustomerViewModelService>();
        services.AddScoped<ICustomerReportViewModelService, CustomerReportViewModelService>();
        services.AddScoped<ICustomerGroupViewModelService, CustomerGroupViewModelService>();
        services.AddScoped<ICustomerTagViewModelService, CustomerTagViewModelService>();
        services.AddScoped<IDiscountViewModelService, DiscountViewModelService>();
        services.AddScoped<IDocumentViewModelService, DocumentViewModelService>();
        services.AddScoped<IEmailAccountViewModelService, EmailAccountViewModelService>();
        services.AddScoped<IGiftVoucherViewModelService, GiftVoucherViewModelService>();
        services.AddScoped<IKnowledgebaseViewModelService, KnowledgebaseViewModelService>();
        services.AddScoped<ILanguageViewModelService, LanguageViewModelService>();
        services.AddScoped<ICollectionViewModelService, CollectionViewModelService>();
        services.AddScoped<INewsViewModelService, NewsViewModelService>();
        services.AddScoped<IOrderViewModelService, OrderViewModelService>();
        services.AddScoped<IShipmentViewModelService, ShipmentViewModelService>();
        services.AddScoped<IProductReviewViewModelService, ProductReviewViewModelService>();
        services.AddScoped<IMerchandiseReturnViewModelService, MerchandiseReturnViewModelService>();
        services.AddScoped<IVendorViewModelService, VendorViewModelService>();
        services.AddScoped<IPageViewModelService, PageViewModelService>();
        services.AddScoped<IStoreViewModelService, StoreViewModelService>();
        services.AddScoped<IBrandViewModelService, BrandViewModelService>();
        services.AddScoped<IProductViewModelService, ProductViewModelService>();
        services.AddScoped<IPictureViewModelService, PictureViewModelService>();
        services.AddScoped<IElFinderViewModelService, ElFinderViewModelService>();
        services.AddScoped<IMenuViewModelService, MenuViewModelService>();

        services.AddScoped<IAreaViewFactory, AdminAreaViewFactory>();
    }

    public void Configure(WebApplication application, IWebHostEnvironment webHostEnvironment)
    {
    }

    public int Priority => 101;
    public bool BeforeConfigure => false;
}