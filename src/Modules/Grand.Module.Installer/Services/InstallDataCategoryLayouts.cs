﻿using Grand.Domain.Catalog;

namespace Grand.Module.Installer.Services;

public partial class InstallationService
{
    protected virtual Task InstallCategoryLayouts()
    {
        var categoryLayouts = new List<CategoryLayout> {
            new() {
                Name = "Grid or Lines",
                ViewPath = "CategoryLayout.GridOrLines",
                DisplayOrder = 1
            }
        };
        categoryLayouts.ForEach(x => _categoryLayoutRepository.Insert(x));
        return Task.CompletedTask;
    }
}