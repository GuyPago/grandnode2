﻿using Grand.Business.Core.Dto;
using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Catalog.Collections;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Stores;
using Grand.Business.Core.Interfaces.ExportImport;
using Grand.Domain.Permissions;
using Grand.Domain.Catalog;
using Grand.Infrastructure;
using Grand.Web.Admin.Extensions;
using Grand.Web.Admin.Extensions.Mapping;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Models.Catalog;
using Grand.Web.Admin.Models.Common;
using Grand.Web.Common.DataSource;
using Grand.Web.Common.Filters;
using Grand.Web.Common.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grand.Web.Admin.Controllers;

[PermissionAuthorize(PermissionSystemName.Collections)]
public class CollectionController : BaseAdminController
{
    #region Constructors

    public CollectionController(
        ICollectionViewModelService collectionViewModelService,
        ICollectionService collectionService,
        IContextAccessor contextAccessor,
        IStoreService storeService,
        ILanguageService languageService,
        ITranslationService translationService,
        IGroupService groupService,
        IPictureViewModelService pictureViewModelService)
    {
        _collectionViewModelService = collectionViewModelService;
        _collectionService = collectionService;
        _contextAccessor = contextAccessor;
        _storeService = storeService;
        _languageService = languageService;
        _translationService = translationService;
        _groupService = groupService;
        _pictureViewModelService = pictureViewModelService;
    }

    #endregion

    #region Utilities

    protected async Task<(bool allow, string message)> CheckAccessToCollection(Collection collection)
    {
        if (collection == null) return (false, "Collection not exists");
        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!(!collection.LimitedToStores ||
                  (collection.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                   collection.LimitedToStores)))
                return (false, "This is not your collection");
        return (true, null);
    }

    #endregion

    #region Fields

    private readonly ICollectionViewModelService _collectionViewModelService;
    private readonly ICollectionService _collectionService;
    private readonly IContextAccessor _contextAccessor;
    private readonly IStoreService _storeService;
    private readonly ILanguageService _languageService;
    private readonly ITranslationService _translationService;
    private readonly IGroupService _groupService;
    private readonly IPictureViewModelService _pictureViewModelService;

    #endregion

    #region List

    public IActionResult Index()
    {
        return RedirectToAction("List");
    }

    public async Task<IActionResult> List()
    {
        var storeId = _contextAccessor.WorkContext.CurrentCustomer.StaffStoreId;
        var model = new CollectionListModel();
        model.AvailableStores.Add(new SelectListItem
            { Text = _translationService.GetResource("Admin.Common.All"), Value = "" });
        foreach (var s in (await _storeService.GetAllStores()).Where(x =>
                     x.Id == storeId || string.IsNullOrWhiteSpace(storeId)))
            model.AvailableStores.Add(new SelectListItem { Text = s.Shortcut, Value = s.Id });

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> List(DataSourceRequest command, CollectionListModel model)
    {
        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            model.SearchStoreId = _contextAccessor.WorkContext.CurrentCustomer.StaffStoreId;
        var collections = await _collectionService.GetAllCollections(model.SearchCollectionName,
            model.SearchStoreId, command.Page - 1, command.PageSize, true);
        var gridModel = new DataSourceResult {
            Data = collections.Select(x => x.ToModel()),
            Total = collections.TotalCount
        };

        return Json(gridModel);
    }

    #endregion

    #region Create / Edit / Delete

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    public async Task<IActionResult> Create([FromServices] CatalogSettings catalogSettings)
    {
        var model = new CollectionModel();
        //locales
        await AddLocales(_languageService, model.Locales);
        //layouts
        await _collectionViewModelService.PrepareLayoutsModel(model);
        //discounts
        await _collectionViewModelService.PrepareDiscountModel(model, null, true);
        //default values
        model.PageSize = catalogSettings.DefaultCollectionPageSize;
        model.PageSizeOptions = catalogSettings.DefaultCollectionPageSizeOptions;
        model.Published = true;
        model.AllowCustomersToSelectPageSize = true;
        //sort options
        _collectionViewModelService.PrepareSortOptionsModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Create(CollectionModel model, bool continueEditing)
    {
        if (ModelState.IsValid)
        {
            if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
                model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];

            var collection = await _collectionViewModelService.InsertCollectionModel(model);
            Success(_translationService.GetResource("Admin.Catalog.Collections.Added"));
            return continueEditing ? RedirectToAction("Edit", new { id = collection.Id }) : RedirectToAction("List");
        }

        //If we got this far, something failed, redisplay form
        //layouts
        await _collectionViewModelService.PrepareLayoutsModel(model);
        //discounts
        await _collectionViewModelService.PrepareDiscountModel(model, null, true);
        //sort options
        _collectionViewModelService.PrepareSortOptionsModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    public async Task<IActionResult> Edit(string id)
    {
        var collection = await _collectionService.GetCollectionById(id);
        if (collection == null)
            //No collection found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
        {
            if (!collection.LimitedToStores || (collection.LimitedToStores &&
                                                collection.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                                                collection.Stores.Count > 1))
            {
                Warning(_translationService.GetResource("Admin.Catalog.Collections.Permissions"));
            }
            else
            {
                if (!collection.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                    return RedirectToAction("List");
            }
        }

        var model = collection.ToModel();
        //locales
        await AddLocales(_languageService, model.Locales, (locale, languageId) =>
        {
            locale.Name = collection.GetTranslation(x => x.Name, languageId, false);
            locale.Description = collection.GetTranslation(x => x.Description, languageId, false);
            locale.BottomDescription = collection.GetTranslation(x => x.BottomDescription, languageId, false);
            locale.MetaKeywords = collection.GetTranslation(x => x.MetaKeywords, languageId, false);
            locale.MetaDescription = collection.GetTranslation(x => x.MetaDescription, languageId, false);
            locale.MetaTitle = collection.GetTranslation(x => x.MetaTitle, languageId, false);
            locale.SeName = collection.GetSeName(languageId, false);
        });
        //layouts
        await _collectionViewModelService.PrepareLayoutsModel(model);
        //discounts
        await _collectionViewModelService.PrepareDiscountModel(model, collection, false);
        //sort options
        _collectionViewModelService.PrepareSortOptionsModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Edit(CollectionModel model, bool continueEditing)
    {
        var collection = await _collectionService.GetCollectionById(model.Id);
        if (collection == null)
            //No collection found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!collection.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = collection.Id });
        if (ModelState.IsValid)
        {
            if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
                model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];
            collection = await _collectionViewModelService.UpdateCollectionModel(collection, model);
            Success(_translationService.GetResource("Admin.Catalog.Collections.Updated"));

            if (continueEditing)
            {
                //selected tab
                await SaveSelectedTabIndex();

                return RedirectToAction("Edit", new { id = collection.Id });
            }

            return RedirectToAction("List");
        }


        //If we got this far, something failed, redisplay form
        //layouts
        await _collectionViewModelService.PrepareLayoutsModel(model);
        //discounts
        await _collectionViewModelService.PrepareDiscountModel(model, collection, true);
        //sort options
        _collectionViewModelService.PrepareSortOptionsModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var collection = await _collectionService.GetCollectionById(id);
        if (collection == null)
            //No collection found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!collection.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = collection.Id });

        if (ModelState.IsValid)
        {
            await _collectionViewModelService.DeleteCollection(collection);

            Success(_translationService.GetResource("Admin.Catalog.Collections.Deleted"));
            return RedirectToAction("List");
        }

        Error(ModelState);
        return RedirectToAction("Edit", new { id = collection.Id });
    }

    #endregion

    #region Picture

    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    public async Task<IActionResult> PicturePopup(string collectionId)
    {
        var collection = await _collectionService.GetCollectionById(collectionId);
        if (collection == null)
            return Content("Collection not exist");

        if (string.IsNullOrEmpty(collection.PictureId))
            return Content("Picture not exist");

        var permission = await CheckAccessToCollection(collection);
        if (!permission.allow)
            return Content(permission.message);

        return View("Partials/PicturePopup",
            await _pictureViewModelService.PreparePictureModel(collection.PictureId, collection.Id));
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> PicturePopup(PictureModel model)
    {
        if (ModelState.IsValid)
        {
            var collection = await _collectionService.GetCollectionById(model.ObjectId);
            if (collection == null)
                throw new ArgumentException("No collection found with the specified id");

            var permission = await CheckAccessToCollection(collection);
            if (!permission.allow)
                return Content(permission.message);

            if (string.IsNullOrEmpty(collection.PictureId))
                throw new ArgumentException("No picture found with the specified id");

            if (collection.PictureId != model.Id)
                throw new ArgumentException("Picture ident doesn't fit with collection");

            await _pictureViewModelService.UpdatePicture(model);

            return Content("");
        }

        Error(ModelState);

        return View("Partials/PicturePopup", model);
    }

    #endregion

    #region Export / Import

    [PermissionAuthorizeAction(PermissionActionName.Export)]
    public async Task<IActionResult> ExportXlsx([FromServices] IExportManager<Collection> exportManager)
    {
        try
        {
            var bytes = await exportManager.Export(await _collectionService.GetAllCollections(showHidden: true,
                storeId: _contextAccessor.WorkContext.CurrentCustomer.StaffStoreId));
            return File(bytes, "text/xls", "collections.xlsx");
        }
        catch (Exception exc)
        {
            Error(exc);
            return RedirectToAction("List");
        }
    }

    [PermissionAuthorizeAction(PermissionActionName.Import)]
    [HttpPost]
    public async Task<IActionResult> ImportFromXlsx(IFormFile importexcelfile,
        [FromServices] IImportManager<CollectionDto> importManager)
    {
        try
        {
            if (importexcelfile is { Length: > 0 })
            {
                await importManager.Import(importexcelfile.OpenReadStream());
            }
            else
            {
                Error(_translationService.GetResource("Admin.Common.UploadFile"));
                return RedirectToAction("List");
            }

            Success(_translationService.GetResource("Admin.Catalog.Collection.Imported"));
            return RedirectToAction("List");
        }
        catch (Exception exc)
        {
            Error(exc);
            return RedirectToAction("List");
        }
    }

    #endregion

    #region Products

    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    [HttpPost]
    public async Task<IActionResult> ProductList(DataSourceRequest command, string collectionId)
    {
        var collection = await _collectionService.GetCollectionById(collectionId);
        var permission = await CheckAccessToCollection(collection);
        if (!permission.allow)
            return ErrorForKendoGridJson(permission.message);

        var (collectionProductModels, totalCount) =
            await _collectionViewModelService.PrepareCollectionProductModel(collectionId, _contextAccessor.StoreContext.CurrentStore.Id,
                command.Page, command.PageSize);

        var gridModel = new DataSourceResult {
            Data = collectionProductModels.ToList(),
            Total = totalCount
        };
        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> ProductUpdate(CollectionModel.CollectionProductModel model)
    {
        if (ModelState.IsValid)
        {
            await _collectionViewModelService.ProductUpdate(model);
            return new JsonResult("");
        }

        return ErrorForKendoGridJson(ModelState);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> ProductDelete(CollectionModel.CollectionProductModel model)
    {
        if (ModelState.IsValid)
        {
            await _collectionViewModelService.ProductDelete(model.Id, model.ProductId);
            return new JsonResult("");
        }

        return ErrorForKendoGridJson(ModelState);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    public async Task<IActionResult> ProductAddPopup(string collectionId)
    {
        var model = await _collectionViewModelService.PrepareAddCollectionProductModel(_contextAccessor.WorkContext.CurrentCustomer
            .StaffStoreId);
        model.CollectionId = collectionId;
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> ProductAddPopupList(DataSourceRequest command,
        CollectionModel.AddCollectionProductModel model)
    {
        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            model.SearchStoreId = _contextAccessor.WorkContext.CurrentCustomer.StaffStoreId;
        var products = await _collectionViewModelService.PrepareProductModel(model, command.Page, command.PageSize);
        var gridModel = new DataSourceResult {
            Data = products.products.ToList(),
            Total = products.totalCount
        };

        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> ProductAddPopup(CollectionModel.AddCollectionProductModel model)
    {
        if (ModelState.IsValid)
        {
            if (model.SelectedProductIds != null) await _collectionViewModelService.InsertCollectionProductModel(model);
            return Content("");
        }

        Error(ModelState);
        return View(model);
    }

    #endregion
}