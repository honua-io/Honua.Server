# TODO/FIXME Catalog

**Generated:** 2025-11-06
**Total TODOs Found:** 121

## Executive Summary

This document catalogs all TODO and FIXME comments in the Honua.Server codebase. These represent incomplete features, technical debt, and future improvements that need attention.

### Statistics

- **Total Items:** 121
- **Critical Priority:** 0
- **High Priority:** 18
- **Medium Priority:** 103
- **Low Priority:** 0

### Quick Wins Available
**22 items** can be completed in less than 30 minutes (Simple fixes with High/Medium priority)

---

## Distribution by Category

| Category | Count | Status |
|----------|-------|--------|
| Technical Debt | 83 | Refactor/Improve |
| Features | 38 | Implement |
| Security | 0 | **ACTION REQUIRED** |
| Performance | 0 | Optimize |

## HIGH PRIORITY (18 items)

**Next release should include these.**

| File | Line | Text | Complexity |
|------|------|------|-----------|
| `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor` | 649 | // TODO: Create layer with all configurations | Simple fix |
| `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs` | 87 | const int maxConcurrent = 10; // TODO: Make configurable per tenant | Simple fix |
| `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs` | 101 | const int rateLimit = 100; // TODO: Make configurable per tenant | Simple fix |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor` | 366 | // TODO: Replace with actual API calls when backend is implemented | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor` | 206 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 300 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 307 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 350 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 356 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 379 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor` | 551 | // TODO: Call backend endpoint to fetch preview data | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor` | 570 | // TODO: Replace with actual backend API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor` | 616 | // TODO: Call backend endpoint to get filtered count | Medium refactor |
| `Honua.Admin.Blazor/Components/Pages/ServiceDetail.razor` | 707 | // TODO: Implement save to backend | Medium refactor |
| `Honua.Admin.Blazor/Components/Shared/AlertRuleDialog.razor` | 114 | // TODO: Replace with actual API call | Medium refactor |
| `Honua.Admin.Blazor/Components/Shared/WhereClauseBuilder.razor` | 300 | // TODO: Call backend endpoint to validate query against actual databa | Medium refactor |
| `Honua.Server.Enterprise/Geoprocessing/GeoprocessingWorkerService.cs` | 161 | // TODO: Update progress in database | Medium refactor |
| `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs` | 154 | // TODO: Load from database | Medium refactor |


---

## QUICK WINS - Start Here! (22 items)

**These can be fixed quickly. Great for new contributors or sprint velocities.**

1. **Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:649**
   - Priority: High
   - Text: // TODO: Create layer with all configurations

2. **Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:87**
   - Priority: High
   - Text: const int maxConcurrent = 10; // TODO: Make configurable per tenant

3. **Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:101**
   - Priority: High
   - Text: const int rateLimit = 100; // TODO: Make configurable per tenant

4. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:404**
   - Priority: Medium
   - Text: // TODO: Open dialog to create new alert rule

5. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:410**
   - Priority: Medium
   - Text: // TODO: Open dialog to edit alert rule

6. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:456**
   - Priority: Medium
   - Text: // TODO: Open dialog to create notification channel

7. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:462**
   - Priority: Medium
   - Text: // TODO: Open dialog to edit notification channel

8. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:508**
   - Priority: Medium
   - Text: // TODO: Open dialog to show full alert details

9. **Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:281**
   - Priority: Medium
   - Text: // TODO: Open dialog with full alert details

10. **Honua.Admin.Blazor/Components/Pages/DataSourceList.razor:283**
   - Priority: Medium
   - Text: // TODO: Implement table browser dialog

11. **Honua.Admin.Blazor/Components/Pages/LayerEditor.razor:282**
   - Priority: Medium
   - Text: // TODO: Show dialog for custom CRS input

12. **Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:667**
   - Priority: Medium
   - Text: // TODO: Show advanced filter builder dialog

13. **Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:673**
   - Priority: Medium
   - Text: // TODO: Show filter presets dialog

14. **Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:846**
   - Priority: Medium
   - Text: // TODO: Show bulk edit dialog

15. **Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:852**
   - Priority: Medium
   - Text: // TODO: Show calculate field dialog

16. **Honua.MapSDK/Components/Bookmarks/HonuaBookmarks.razor:553**
   - Priority: Medium
   - Text: // TODO: Show confirmation dialog

17. **Honua.MapSDK/Components/Bookmarks/HonuaBookmarks.razor:583**
   - Priority: Medium
   - Text: // TODO: Show success message

18. **Honua.MapSDK/Components/Bookmarks/HonuaBookmarks.razor:623**
   - Priority: Medium
   - Text: // TODO: Show folder options menu

19. **Honua.MapSDK/Components/Bookmarks/HonuaBookmarks.razor:664**
   - Priority: Medium
   - Text: // TODO: Show file picker and import

20. **Honua.Server.Core/Import/Validation/FeatureSchemaValidator.cs:622**
   - Priority: Medium
   - Text: // TODO: Could add more detailed GeoJSON validation here



---

## FEATURE BACKLOG (38 items)

### UI/Dialog TODOs (Blazor Components)
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:366` - // TODO: Replace with actual API calls when backend is implemented
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:206` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:300` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:307` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:350` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:356` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:379` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:551` - // TODO: Call backend endpoint to fetch preview data
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:570` - // TODO: Replace with actual backend API call
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:616` - // TODO: Call backend endpoint to get filtered count
- `Honua.Admin.Blazor/Components/Pages/ServiceDetail.razor:707` - // TODO: Implement save to backend
- `Honua.Admin.Blazor/Components/Shared/AlertRuleDialog.razor:114` - // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Shared/WhereClauseBuilder.razor:300` - // TODO: Call backend endpoint to validate query against actual database
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:404` - // TODO: Open dialog to create new alert rule
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:410` - // TODO: Open dialog to edit alert rule
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:456` - // TODO: Open dialog to create notification channel
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:462` - // TODO: Open dialog to edit notification channel
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:508` - // TODO: Open dialog to show full alert details
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:281` - // TODO: Open dialog with full alert details
- `Honua.Admin.Blazor/Components/Pages/DataSourceList.razor:283` - // TODO: Implement table browser dialog
- `Honua.Admin.Blazor/Components/Pages/LayerEditor.razor:282` - // TODO: Show dialog for custom CRS input
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:667` - // TODO: Show advanced filter builder dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:673` - // TODO: Show filter presets dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:846` - // TODO: Show bulk edit dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:852` - // TODO: Show calculate field dialog


---

## TECHNICAL DEBT (83 items)

### Phase 2 OGC Handlers Refactoring
**These are architectural improvements for separating OGC handler implementations.**

**Count:** 29 items (12 OGC handler refactoring tasks)

### Configuration/Tenancy Improvements
**Make configurable per tenant or environment.**

- `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:87` - const int maxConcurrent = 10; // TODO: Make configurable per tenant
- `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:101` - const int rateLimit = 100; // TODO: Make configurable per tenant


---

## Analysis by Complexity

### Simple Fix (22)
**Under 30 minutes to complete**

- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:649` [High] // TODO: Create layer with all configurations
- `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:87` [High] const int maxConcurrent = 10; // TODO: Make configurable per tenant
- `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:101` [High] const int rateLimit = 100; // TODO: Make configurable per tenant
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:404` [Medium] // TODO: Open dialog to create new alert rule
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:410` [Medium] // TODO: Open dialog to edit alert rule
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:456` [Medium] // TODO: Open dialog to create notification channel
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:462` [Medium] // TODO: Open dialog to edit notification channel
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:508` [Medium] // TODO: Open dialog to show full alert details
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:281` [Medium] // TODO: Open dialog with full alert details
- `Honua.Admin.Blazor/Components/Pages/DataSourceList.razor:283` [Medium] // TODO: Implement table browser dialog
- `Honua.Admin.Blazor/Components/Pages/LayerEditor.razor:282` [Medium] // TODO: Show dialog for custom CRS input
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:667` [Medium] // TODO: Show advanced filter builder dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:673` [Medium] // TODO: Show filter presets dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:846` [Medium] // TODO: Show bulk edit dialog
- `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs:852` [Medium] // TODO: Show calculate field dialog


### Medium Refactor (70)
**1-4 hours to complete**

- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor:366` [High] // TODO: Replace with actual API calls when backend is implemented
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertHistory.razor:206` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:300` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:307` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:350` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:356` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor:379` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:551` [High] // TODO: Call backend endpoint to fetch preview data
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:570` [High] // TODO: Replace with actual backend API call
- `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor:616` [High] // TODO: Call backend endpoint to get filtered count
- `Honua.Admin.Blazor/Components/Pages/ServiceDetail.razor:707` [High] // TODO: Implement save to backend
- `Honua.Admin.Blazor/Components/Shared/AlertRuleDialog.razor:114` [High] // TODO: Replace with actual API call
- `Honua.Admin.Blazor/Components/Shared/WhereClauseBuilder.razor:300` [High] // TODO: Call backend endpoint to validate query against actual databa
- `Honua.Server.Enterprise/Geoprocessing/GeoprocessingWorkerService.cs:161` [High] // TODO: Update progress in database
- `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs:154` [High] // TODO: Load from database


### Complex Feature (29)
**Multiple hours/days to complete**

- `Honua.Server.Host/Ogc/IWcsHandler.cs:18` [Medium] /// TODO Phase 2: Extract WCS-specific methods from OgcSharedHandlers.
- `Honua.Server.Host/Ogc/IWcsHandler.cs:34` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWcsHandler.cs:49` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWcsHandler.cs:64` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWfsHandler.cs:18` [Medium] /// TODO Phase 2: Extract WFS-specific methods from OgcSharedHandlers.
- `Honua.Server.Host/Ogc/IWfsHandler.cs:37` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWfsHandler.cs:52` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWfsHandler.cs:67` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWfsHandler.cs:82` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWmsHandler.cs:18` [Medium] /// TODO Phase 2: Extract WMS-specific methods from OgcSharedHandlers.
- `Honua.Server.Host/Ogc/IWmsHandler.cs:36` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWmsHandler.cs:51` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWmsHandler.cs:66` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWmtsHandler.cs:18` [Medium] /// TODO Phase 2: Extract WMTS-specific methods from OgcSharedHandlers
- `Honua.Server.Host/Ogc/IWmtsHandler.cs:34` [Medium] /// TODO Phase 2: Move implementation from OgcSharedHandlers


---

## Recommendations

### Immediate Actions (This Sprint)
1. **Address all CRITICAL items** in the Security section
2. **Create GitHub issues** for each Critical/High priority TODO
3. **Schedule QA review** for authorization-related TODOs

### Short Term (Next Sprint)
1. Tackle the **Quick Wins** for quick velocity boost
2. Implement **middleware extensions** for security
3. Complete **authorization** integration across admin endpoints

### Medium Term (Next 2 Sprints)
1. Refactor **OGC handlers** (Phase 2 architecture)
2. Implement **hard delete functionality** across data stores
3. Complete **feature backlog** for Blazor UI components

### Long Term (Roadmap)
1. GitOps feature implementation
2. Advanced geoprocessing validations
3. Performance optimizations (caching, lazy loading)

---

## File-by-File Summary

### High TODO Density Files (Most work needed)

| File | TODO Count |
|------|-----------|
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertConfiguration.razor` | 10 |
| `Honua.Server.Host/Admin/AlertAdministrationEndpoints.cs` | 9 |
| `Honua.Server.Enterprise/Geoprocessing/PostgresControlPlane.cs` | 6 |
| `Honua.MapSDK/Components/AttributeTable/HonuaAttributeTable.razor.cs` | 6 |
| `Honua.Admin.Blazor/Components/Pages/Alerts/AlertRuleEditor.razor` | 5 |
| `Honua.Server.Host/Ogc/IWfsHandler.cs` | 5 |
| `Honua.Admin.Blazor/Components/Pages/LayerCreationWizard.razor` | 4 |
| `Honua.MapSDK/Components/Bookmarks/HonuaBookmarks.razor` | 4 |
| `Honua.MapSDK/Components/DataGrid/HonuaDataGrid.razor.cs` | 4 |
| `Honua.Server.Host/Ogc/IWcsHandler.cs` | 4 |
| `Honua.Server.Host/Ogc/IWmsHandler.cs` | 4 |
| `Honua.Server.Host/Ogc/IWmtsHandler.cs` | 4 |
| `Honua.Server.Host/Ogc/WcsHandlers.cs` | 4 |
| `Honua.Server.Host/Ogc/WmsHandlers.cs` | 4 |
| `Honua.Server.Host/Ogc/WmtsHandlers.cs` | 4 |


---

## Implementation Strategy

### Phase 1: Critical Security Fixes (1-2 days)
- [ ] Add authorization checks to all admin endpoints
- [ ] Implement CSRF validation middleware
- [ ] Add security policy middleware

### Phase 2: Quick Wins (3-5 days)
- [ ] Make configuration values per-tenant
- [ ] Implement UI dialog handlers
- [ ] Complete validation stubs

### Phase 3: Feature Completion (1-2 weeks)
- [ ] API call implementations
- [ ] Middleware implementations
- [ ] Database integration

### Phase 4: Technical Debt (2-4 weeks)
- [ ] OGC handler refactoring
- [ ] Hard delete functionality
- [ ] Service registration cleanup

---

## Tracking Progress

To track completion:
1. Create GitHub issue for each TODO
2. Update TODO comments with issue number: `// TODO (GH-123): description`
3. When completed, remove TODO or mark as: `// DONE: description`
4. Generate updated catalog monthly

---

*Last Updated: 2025-11-06*
*Review this document monthly and update priorities based on business goals*

