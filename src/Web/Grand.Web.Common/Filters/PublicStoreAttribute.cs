﻿using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Permissions;
using Grand.Data;
using Grand.Domain.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Grand.Web.Common.Filters;

/// <summary>
///     Represents a filter attribute that confirms access to public store
/// </summary>
public class PublicStoreAttribute : TypeFilterAttribute
{
    /// <summary>
    ///     Create instance of the filter attribute
    /// </summary>
    /// <param name="ignore">Whether to ignore the execution of filter actions</param>
    public PublicStoreAttribute(bool ignore = false) : base(typeof(AccessPublicStoreFilter))
    {
        IgnoreFilter = ignore;
        Arguments = [ignore];
    }

    public bool IgnoreFilter { get; }

    #region Filter

    /// <summary>
    ///     Represents a filter that confirms access to public store
    /// </summary>
    private class AccessPublicStoreFilter(bool ignoreFilter, IPermissionService permissionService,
        StoreInformationSettings storeInformationSettings) : IAsyncAuthorizationFilter
    {

        #region Methods

        /// <summary>
        ///     Called early in the filter pipeline to confirm request is authorized
        /// </summary>
        /// <param name="filterContext">Authorization filter context</param>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext filterContext)
        {
            //ignore filter (the action available even when navigation is not allowed)
            ArgumentNullException.ThrowIfNull(filterContext);

            //check whether this filter has been overridden for the Action
            var actionFilter = filterContext.ActionDescriptor.FilterDescriptors
                .Where(f => f.Scope == FilterScope.Action)
                .Select(f => f.Filter).OfType<PublicStoreAttribute>().FirstOrDefault();


            //ignore filter (the action is available even if navigation is not allowed)
            if (actionFilter?.IgnoreFilter ?? ignoreFilter)
                return;

            if (!DataSettingsManager.DatabaseIsInstalled())
                return;

            //check whether current customer has access to a public store
            if (await permissionService.Authorize(StandardPermission.PublicStoreAllowNavigation))
                return;

            filterContext.Result = storeInformationSettings.StoreClosed
                ? new RedirectToRouteResult("StoreClosed", new RouteValueDictionary())
                :
                //customer has not access to a public store
                new RedirectToRouteResult("Login", new RouteValueDictionary());
        }

        #endregion
    }

    #endregion
}