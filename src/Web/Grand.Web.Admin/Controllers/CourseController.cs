﻿using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Marketing.Courses;
using Grand.Domain.Permissions;
using Grand.Domain.Courses;
using Grand.Infrastructure;
using Grand.Web.Admin.Extensions;
using Grand.Web.Admin.Extensions.Mapping;
using Grand.Web.Admin.Interfaces;
using Grand.Web.Admin.Models.Courses;
using Grand.Web.Common.DataSource;
using Grand.Web.Common.Extensions;
using Grand.Web.Common.Filters;
using Grand.Web.Common.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Web.Admin.Controllers;

[PermissionAuthorize(PermissionSystemName.Courses)]
public class CourseController : BaseAdminController
{
    private readonly ICourseLessonService _courseLessonService;
    private readonly ICourseLevelService _courseLevelService;
    private readonly ICourseService _courseService;
    private readonly ICourseSubjectService _courseSubjectService;
    private readonly ICourseViewModelService _courseViewModelService;
    private readonly IGroupService _groupService;
    private readonly ILanguageService _languageService;

    private readonly ITranslationService _translationService;
    private readonly IContextAccessor _contextAccessor;

    public CourseController(
        ITranslationService translationService,
        ICourseLevelService courseLevelService,
        ICourseService courseService,
        ICourseSubjectService courseSubjectService,
        ICourseLessonService courseLessonService,
        ICourseViewModelService courseViewModelService,
        IContextAccessor contextAccessor,
        ILanguageService languageService,
        IGroupService groupService)
    {
        _translationService = translationService;
        _courseLevelService = courseLevelService;
        _courseService = courseService;
        _courseSubjectService = courseSubjectService;
        _courseLessonService = courseLessonService;
        _courseViewModelService = courseViewModelService;
        _contextAccessor = contextAccessor;
        _languageService = languageService;
        _groupService = groupService;
    }


    #region Level

    public IActionResult Level()
    {
        return View();
    }

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> Levels(DataSourceRequest command)
    {
        var levelModel = (await _courseLevelService.GetAll())
            .Select(x => x.ToModel());

        var gridModel = new DataSourceResult {
            Data = levelModel,
            Total = levelModel.Count()
        };
        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> LevelUpdate(CourseLevelModel model)
    {
        if (!ModelState.IsValid) return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

        var level = await _courseLevelService.GetById(model.Id);
        level = model.ToEntity(level);
        await _courseLevelService.Update(level);

        return new JsonResult("");
    }

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    [HttpPost]
    public async Task<IActionResult> LevelAdd(CourseLevelModel model)
    {
        if (!ModelState.IsValid) return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

        var level = new CourseLevel();
        level = model.ToEntity(level);
        await _courseLevelService.Insert(level);

        return new JsonResult("");
    }

    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> LevelDelete(string id)
    {
        var level = await _courseLevelService.GetById(id);
        if (level == null)
            throw new ArgumentException("No level found with the specified id");

        await _courseLevelService.Delete(level);

        return new JsonResult("");
    }

    #endregion

    #region Course

    public IActionResult List()
    {
        return View();
    }

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> List(DataSourceRequest command)
    {
        var courses = await _courseService.GetAll(command.Page - 1, command.PageSize);

        var gridModel = new DataSourceResult {
            Data = courses,
            Total = courses.TotalCount
        };
        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    public async Task<IActionResult> Create()
    {
        var model = await _courseViewModelService.PrepareCourseModel();
        //locales
        await AddLocales(_languageService, model.Locales);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Create(CourseModel model, bool continueEditing)
    {
        if (ModelState.IsValid)
        {
            if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
                model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];

            var course = await _courseViewModelService.InsertCourseModel(model);
            Success(_translationService.GetResource("Admin.Courses.Course.Added"));
            return continueEditing ? RedirectToAction("Edit", new { id = course.Id }) : RedirectToAction("List");
        }

        //If we got this far, something failed, redisplay form
        model = await _courseViewModelService.PrepareCourseModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    public async Task<IActionResult> Edit(string id)
    {
        var course = await _courseService.GetById(id);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
        {
            if (!course.LimitedToStores || (course.LimitedToStores &&
                                            course.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                                            course.Stores.Count > 1))
            {
                Warning(_translationService.GetResource("Admin.Courses.Course.Permissions"));
            }
            else
            {
                if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                    return RedirectToAction("List");
            }
        }

        var model = course.ToModel();
        //locales
        await AddLocales(_languageService, model.Locales, (locale, languageId) =>
        {
            locale.Name = course.GetTranslation(x => x.Name, languageId, false);
            locale.ShortDescription = course.GetTranslation(x => x.ShortDescription, languageId, false);
            locale.Description = course.GetTranslation(x => x.Description, languageId, false);
            locale.MetaKeywords = course.GetTranslation(x => x.MetaKeywords, languageId, false);
            locale.MetaDescription = course.GetTranslation(x => x.MetaDescription, languageId, false);
            locale.MetaTitle = course.GetTranslation(x => x.MetaTitle, languageId, false);
            locale.SeName = course.GetSeName(languageId, false);
        });

        model = await _courseViewModelService.PrepareCourseModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> Edit(CourseModel model, bool continueEditing)
    {
        var course = await _courseService.GetById(model.Id);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = course.Id });

        if (ModelState.IsValid)
        {
            if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
                model.Stores = [_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId];

            course = await _courseViewModelService.UpdateCourseModel(course, model);

            Success(_translationService.GetResource("Admin.Courses.Course.Updated"));
            if (continueEditing)
            {
                //selected tab
                await SaveSelectedTabIndex();

                return RedirectToAction("Edit", new { id = course.Id });
            }

            return RedirectToAction("List");
        }

        //If we got this far, something failed, redisplay form
        model = await _courseViewModelService.PrepareCourseModel(model);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var course = await _courseService.GetById(id);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = course.Id });

        if (ModelState.IsValid)
        {
            await _courseViewModelService.DeleteCourse(course);
            Success(_translationService.GetResource("Admin.Courses.Course.Deleted"));
        }

        return RedirectToAction("List");
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    public async Task<IActionResult> AssociateProductToCoursePopup()
    {
        var model = await _courseViewModelService.PrepareAssociateProductToCourseModel();
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> AssociateProductToCoursePopupList(DataSourceRequest command,
        CourseModel.AssociateProductToCourseModel model)
    {
        var products = await _courseViewModelService.PrepareProductModel(model, command.Page, command.PageSize);
        var gridModel = new DataSourceResult {
            Data = products.products.ToList(),
            Total = products.totalCount
        };

        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> AssociateProductToCoursePopup(string btnId, string productIdInput,
        string productNameInput, CourseModel.AssociateProductToCourseModel model,
        [FromServices] IProductService productService)
    {
        var associatedProduct = await productService.GetProductById(model.AssociatedToProductId);
        if (associatedProduct == null)
            return Content("Cannot load a product");

        ViewBag.RefreshPage = true;
        ViewBag.productIdInput = productIdInput;
        ViewBag.productNameInput = productNameInput;
        ViewBag.btnId = btnId;
        ViewBag.productId = associatedProduct.Id;
        ViewBag.productName = associatedProduct.Name;
        return View(model);
    }

    #endregion

    #region Subjects

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> Subjects(DataSourceRequest command, string courseId)
    {
        var subjectModel = (await _courseSubjectService.GetByCourseId(courseId))
            .Select(x => x.ToModel());

        var gridModel = new DataSourceResult {
            Data = subjectModel,
            Total = subjectModel.Count()
        };
        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    public async Task<IActionResult> SubjectUpdate(CourseSubjectModel model)
    {
        if (!ModelState.IsValid) return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

        var subject = await _courseSubjectService.GetById(model.Id);
        subject = model.ToEntity(subject);
        await _courseSubjectService.Update(subject);

        return new JsonResult("");
    }

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    [HttpPost]
    public async Task<IActionResult> SubjectAdd(CourseSubjectModel model)
    {
        if (!ModelState.IsValid) return Json(new DataSourceResult { Errors = ModelState.SerializeErrors() });

        var subject = new CourseSubject();
        subject = model.ToEntity(subject);
        await _courseSubjectService.Insert(subject);

        return new JsonResult("");
    }

    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> SubjectDelete(string id)
    {
        var subject = await _courseSubjectService.GetById(id);
        if (subject == null)
            throw new ArgumentException("No subject found with the specified id");

        await _courseSubjectService.Delete(subject);

        return new JsonResult("");
    }

    #endregion

    #region Lessons

    [PermissionAuthorizeAction(PermissionActionName.List)]
    [HttpPost]
    public async Task<IActionResult> Lessons(string courseId)
    {
        var lessonModel = (await _courseLessonService.GetByCourseId(courseId))
            .Select(x => x.ToModel());

        var gridModel = new DataSourceResult {
            Data = lessonModel,
            Total = lessonModel.Count()
        };
        return Json(gridModel);
    }

    [PermissionAuthorizeAction(PermissionActionName.Create)]
    public async Task<IActionResult> CreateLesson(string courseId)
    {
        var course = await _courseService.GetById(courseId);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
        {
            if (!course.LimitedToStores || (course.LimitedToStores &&
                                            course.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                                            course.Stores.Count > 1))
            {
                Warning(_translationService.GetResource("Admin.Courses.Course.Permissions"));
            }
            else
            {
                if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                    return RedirectToAction("List");
            }
        }

        var model = await _courseViewModelService.PrepareCourseLessonModel(courseId);

        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> CreateLesson(CourseLessonModel model, bool continueEditing)
    {
        var course = await _courseService.GetById(model.CourseId);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
        {
            if (!course.LimitedToStores || (course.LimitedToStores &&
                                            course.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                                            course.Stores.Count > 1))
            {
                Warning(_translationService.GetResource("Admin.Courses.Course.Permissions"));
            }
            else
            {
                if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                    return RedirectToAction("List");
            }
        }

        if (ModelState.IsValid)
        {
            var lesson = await _courseViewModelService.InsertCourseLessonModel(model);
            Success(_translationService.GetResource("Admin.Courses.Course.Lesson.Added"));
            return continueEditing
                ? RedirectToAction("EditLesson", new { id = lesson.Id })
                : RedirectToAction("Edit", new { Id = lesson.CourseId });
        }

        //If we got this far, something failed, redisplay form
        model = await _courseViewModelService.PrepareCourseLessonModel(model.CourseId, model);

        return View(model);
    }


    [PermissionAuthorizeAction(PermissionActionName.Preview)]
    public async Task<IActionResult> EditLesson(string id)
    {
        var lesson = await _courseLessonService.GetById(id);
        if (lesson == null)
            //No course found with the specified id
            return RedirectToAction("List");

        var course = await _courseService.GetById(lesson.CourseId);
        if (course == null)
            //No course found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
        {
            if (!course.LimitedToStores || (course.LimitedToStores &&
                                            course.Stores.Contains(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId) &&
                                            course.Stores.Count > 1))
            {
                Warning(_translationService.GetResource("Admin.Courses.Course.Permissions"));
            }
            else
            {
                if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                    return RedirectToAction("List");
            }
        }

        var model = lesson.ToModel();
        model = await _courseViewModelService.PrepareCourseLessonModel(lesson.CourseId, model);
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Edit)]
    [HttpPost]
    [ArgumentNameFilter(KeyName = "save-continue", Argument = "continueEditing")]
    public async Task<IActionResult> EditLesson(CourseLessonModel model, bool continueEditing)
    {
        var lesson = await _courseLessonService.GetById(model.Id);
        if (lesson == null)
            //No lesson found with the specified id
            return RedirectToAction("List");

        var course = await _courseService.GetById(model.CourseId);
        if (course == null)
            //No category found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = course.Id });
        if (ModelState.IsValid)
        {
            lesson = await _courseViewModelService.UpdateCourseLessonModel(lesson, model);

            Success(_translationService.GetResource("Admin.Courses.Course.Lesson.Updated"));
            if (continueEditing)
            {
                //selected tab
                await SaveSelectedTabIndex();

                return RedirectToAction("EditLesson", new { id = lesson.Id });
            }

            return RedirectToAction("Edit", new { id = course.Id });
        }

        //If we got this far, something failed, redisplay form
        model = await _courseViewModelService.PrepareCourseLessonModel(lesson.CourseId, model);
        return View(model);
    }

    [PermissionAuthorizeAction(PermissionActionName.Delete)]
    [HttpPost]
    public async Task<IActionResult> DeleteLesson(string id)
    {
        var lesson = await _courseLessonService.GetById(id);
        if (lesson == null)
            //No lesson found with the specified id
            return RedirectToAction("List");

        var course = await _courseService.GetById(lesson.CourseId);
        if (course == null)
            //No category found with the specified id
            return RedirectToAction("List");

        if (await _groupService.IsStaff(_contextAccessor.WorkContext.CurrentCustomer))
            if (!course.AccessToEntityByStore(_contextAccessor.WorkContext.CurrentCustomer.StaffStoreId))
                return RedirectToAction("Edit", new { id = course.Id });
        await _courseViewModelService.DeleteCourseLesson(lesson);
        Success(_translationService.GetResource("Admin.Courses.Course.Lesson.Deleted"));

        return RedirectToAction("Edit", new { id = course.Id });
    }

    #endregion
}