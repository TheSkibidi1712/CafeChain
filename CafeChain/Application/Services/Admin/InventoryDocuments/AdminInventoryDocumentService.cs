using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Export;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Index;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Constants;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Approvals;
using CafeChain.Models.Inventories.Documents;
using CafeChain.ViewModels.Admin.InventoryDocuments.Detail;
using CafeChain.ViewModels.Admin.InventoryDocuments.Index;
using CafeChain.ViewModels.Admin.InventoryDocuments.Preview;
using CafeChain.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentService : IAdminInventoryDocumentService
    {
        private readonly IAdminInventoryDocumentRepository _repository;

        private readonly IAdminInventoryDocumentSnapshotService _snapshotService;

        private readonly IAdminInventoryDocumentExportService _exportService;

        private readonly IAdminActorContextAccessor _actorAccessor;

        private readonly IScopeAuthorizationService _scopeAuthorization;

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminInventoryDocumentService(
            IAdminInventoryDocumentRepository repository,
            IAdminInventoryDocumentSnapshotService snapshotService,
            IAdminInventoryDocumentExportService exportService,
            IAdminActorContextAccessor actorAccessor,
            IScopeAuthorizationService scopeAuthorization,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _snapshotService = snapshotService;
            _exportService = exportService;
            _actorAccessor = actorAccessor;
            _scopeAuthorization = scopeAuthorization;
            _httpContextAccessor = httpContextAccessor;
        }

        // =====================================================
        // INDEX
        // =====================================================

        public async Task<PaginatedListViewModel<AdminInventoryDocumentListVM>> GetPagedDocumentsAsync(AdminInventoryDocumentFilterDTO filter)
        {
            var allowedStoreIds = await GetAllowedStoreIdsAsync();
            var actor = GetActor();
            var query =
                BuildFilteredDocumentsQuery(filter)
                    .Where(x => allowedStoreIds.Contains(x.StoreId));

            var totalCount = await query.CountAsync();

            var page =
                filter.Page <= 0
                    ? 1
                    : filter.Page;

            var pageSize =
                filter.PageSize <= 0
                    ? 20
                    : filter.PageSize;

            var rows = await query
                .OrderByDescending(x => x.DocumentDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    Document = new AdminInventoryDocumentListVM
                    {
                        InventoryDocumentId = x.InventoryDocumentId,

                        Code = x.Code,

                        Type = x.Type,

                        Status = x.Status,

                        Purpose = x.Purpose,

                        StoreName = x.Store.Name,

                        PartnerName = x.PartnerName,

                        DocumentDate = x.DocumentDate,

                        FinalAmount = x.FinalAmount,

                        ConfirmedAt = x.ConfirmedAt,

                        NegativeApprovalStatus = x.NegativeApproval == null
                            ? null
                            : x.NegativeApproval.Status
                    },
                    RequesterStaffId = x.NegativeApproval == null
                        ? (int?)null
                        : x.NegativeApproval.RequesterStaffId
                })
                .ToListAsync();

            var items = rows.Select(x =>
            {
                x.Document.NegativeApprovalReviewMessage = GetNegativeApprovalReviewMessage(
                    x.Document.NegativeApprovalStatus,
                    x.Document.Status,
                    x.RequesterStaffId,
                    actor.StaffId,
                    actor.RoleNames);
                x.Document.CanReviewNegativeApproval =
                    x.Document.NegativeApprovalStatus == InventoryNegativeApprovalStatuses.Requested
                    && x.Document.Status == InventoryDocumentStatus.PENDING
                    && x.Document.NegativeApprovalReviewMessage == null;
                return x.Document;
            }).ToList();

            return new PaginatedListViewModel<AdminInventoryDocumentListVM>(items, totalCount, page, pageSize);
        }

        private IQueryable<InventoryDocument> BuildFilteredDocumentsQuery(
            AdminInventoryDocumentFilterDTO filter)
        {
            var query =
                _repository.GetDocumentsQuery();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search =
                    filter.Search.Trim();

                query =
                    query.Where(x =>
                        x.Code.Contains(search)
                        || (x.PartnerName != null
                            && x.PartnerName.Contains(search)));
            }

            if (filter.Type.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Type == filter.Type);
            }

            if (filter.Status.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Status == filter.Status);
            }

            if (filter.Purpose.HasValue)
            {
                query =
                    query.Where(x =>
                        x.Purpose == filter.Purpose);
            }

            if (filter.StoreId.HasValue)
            {
                query =
                    query.Where(x =>
                        x.StoreId == filter.StoreId);
            }

            if (filter.FromDate.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DocumentDate >= filter.FromDate);
            }

            if (filter.ToDate.HasValue)
            {
                query =
                    query.Where(x =>
                        x.DocumentDate <= filter.ToDate);
            }

            return query;
        }

        public async Task<AdminInventoryDocumentIndexVM> GetIndexDataAsync(AdminInventoryDocumentFilterDTO filter)
        {
            var allowedStoreIds = await GetAllowedStoreIdsAsync();
            var query = _repository.GetDocumentsQuery()
                .Where(x => allowedStoreIds.Contains(x.StoreId));

            // =====================================================
            // DASHBOARD
            // KHÔNG BỊ ẢNH HƯỞNG FILTER
            // =====================================================

            var totalDocuments = await query.CountAsync();

            var draftDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.DRAFT);

            var confirmedDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.CONFIRMED);

            var cancelledDocuments = await query.CountAsync(x => x.Status == InventoryDocumentStatus.CANCELLED);

            var thisMonthDocuments = await query.CountAsync(x => x.DocumentDate.Month == DateTime.Today.Month  && x.DocumentDate.Year == DateTime.Today.Year);

            // =====================================================
            // LIST
            // =====================================================

            var documents = await GetPagedDocumentsAsync(filter);

            // =====================================================
            // DROPDOWN
            // =====================================================

            var stores = (await _repository.GetStoreDropdownAsync())
                .Where(x => allowedStoreIds.Contains(x.StoreId))
                .ToList();

            return new AdminInventoryDocumentIndexVM
            {
                Filter = filter,

                Documents = documents,

                TotalDocuments = totalDocuments,

                DraftDocuments = draftDocuments,

                ConfirmedDocuments = confirmedDocuments,

                CancelledDocuments = cancelledDocuments,

                ThisMonthDocuments = thisMonthDocuments,

                Stores = stores
            };
        }

        // =====================================================
        // DETAIL
        // =====================================================

        public async Task<AdminInventoryDocumentDetailVM?> GetDetailAsync(int documentId)
        {
            var document = await _repository.GetDocumentWithDetailsAsync(documentId);

            if (document == null)
            {
                return null;
            }

            if (!await CanAccessStoreAsync(document.StoreId))
                return null;

            var costGaps = await _repository.GetNegativeCostGapsByDocumentAsync(documentId);
            var actor = GetActor();
            var negativeApprovalReviewMessage = GetNegativeApprovalReviewMessage(
                document.NegativeApproval?.Status,
                document.Status,
                document.NegativeApproval?.RequesterStaffId,
                actor.StaffId,
                actor.RoleNames);
            var canReviewNegativeApproval =
                document.NegativeApproval?.Status == InventoryNegativeApprovalStatuses.Requested
                && document.Status == InventoryDocumentStatus.PENDING
                && negativeApprovalReviewMessage == null;

            return new AdminInventoryDocumentDetailVM
            {
                InventoryDocumentId = document.InventoryDocumentId,

                Code = document.Code,

                Type = document.Type,

                Status = document.Status,

                Purpose = document.Purpose,

                DocumentDate = document.DocumentDate,

                RequestKey = document.RequestKey,

                IsProcessing = document.IsProcessing,

                StoreName = document.Store?.Name ?? "",

                StaffName = document.Staff?.FullName ?? "",

                ConfirmedAt = document.ConfirmedAt,

                PartnerType = document.PartnerType,

                PartnerName = document.PartnerName,

                SupplierName = document.Supplier?.Name,

                Note = document.Note,

                NegativeReason = document.NegativeReason,

                CanReviewNegativeApproval = canReviewNegativeApproval,

                NegativeApprovalReviewMessage = negativeApprovalReviewMessage,

                NegativeApproval = document.NegativeApproval == null
                    ? null
                    : new AdminInventoryNegativeApprovalVM
                    {
                        ApprovalId = document.NegativeApproval.InventoryNegativeApprovalId,
                        Status = document.NegativeApproval.Status,
                        RequesterName = document.NegativeApproval.RequesterStaff?.FullName ?? string.Empty,
                        ApproverName = document.NegativeApproval.ApproverStaff?.FullName,
                        Reason = document.NegativeApproval.Reason,
                        ReviewNote = document.NegativeApproval.ReviewNote,
                        PolicyVersion = document.NegativeApproval.PolicyVersion,
                        RequestedAt = document.NegativeApproval.RequestedAt,
                        ReviewedAt = document.NegativeApproval.ReviewedAt,
                        Lines = document.NegativeApproval.Lines
                            .OrderBy(x => x.InventoryNegativeApprovalLineId)
                            .Select(x => new AdminInventoryNegativeApprovalLineVM
                            {
                                IngredientId = x.IngredientId,
                                PreparedItemId = x.PreparedItemId,
                                BeforeQty = x.BeforeQty,
                                IssueQty = x.IssueQty,
                                ProjectedAfterQty = x.ProjectedAfterQty,
                                EffectiveMaxNegativeQty = x.EffectiveMaxNegativeQty
                            })
                            .ToList()
                    },

                CostGaps = costGaps.Select(x => new AdminInventoryCostGapVM
                {
                    GapId = x.InventoryNegativeCostGapId,
                    SourceType = x.SourceType,
                    Status = x.Status,
                    OriginalQuantity = x.OriginalQuantity,
                    OutstandingQuantity = x.OutstandingQuantity,
                    SettledQuantity = x.Settlements.Sum(s => s.Quantity),
                    OccurredAt = x.OccurredAt
                }).ToList(),

                TotalAmount = document.TotalAmount,

                VatAmount = document.VatAmount,

                FinalAmount = document.FinalAmount,

                Details =
                    document.Details
                        .Select(x =>
                            new AdminInventoryDocumentDetailItemVM
                            {
                                IngredientName = x.Ingredient.Name,

                                UnitName = x.Unit.Name,

                                Quantity = x.Quantity,

                                BaseQuantity = x.BaseQuantity,

                                UnitPrice = x.UnitPrice,

                                CostPrice = x.CostPrice,

                                CostAmount = x.CostAmount,

                                TotalAmount = x.TotalAmount,

                                Note = x.Note
                            })
                        .ToList()
            };
        }

        // =====================================================
        // PREVIEW
        // =====================================================

        public async Task<AdminInventoryDocumentPreviewVM?> GetPreviewAsync(int documentId)
        {
            var document = await _repository.GetByIdAsync(documentId);
            if (document == null || !await CanAccessStoreAsync(document.StoreId))
                return null;

            var snapshot = await _repository.GetSnapshotAsync(documentId);

            if (snapshot == null)
            {
                return null;
            }

            return new AdminInventoryDocumentPreviewVM
            {
                Code = snapshot.Code,

                DocumentDate = snapshot.DocumentDate,

                StoreName = snapshot.StoreName,

                StaffName = snapshot.StaffName,

                PartnerName = snapshot.PartnerName,

                TotalAmount = snapshot.TotalAmount,

                VatAmount = snapshot.VatAmount,

                FinalAmount = snapshot.FinalAmount,

                Details =
                    snapshot.Details
                        .Select(x =>
                            new AdminInventoryDocumentPreviewItemVM
                            {
                                ItemName = x.ItemName,

                                UnitName = x.UnitName,

                                Quantity = x.Quantity,

                                UnitPrice = x.UnitPrice,

                                TotalAmount = x.TotalAmount
                            })
                        .ToList()
            };
        }

        // ====================================================
        // EXPORT FILE
        // ====================================================
        public async Task<byte[]?> ExportFileAsync(ExportInventoryDocumentDTO dto)
        {
            var document = await _repository.GetByIdAsync(dto.DocumentId);

            if (document == null)
            {
                return null;
            }

            if (!await CanAccessStoreAsync(document.StoreId))
                return null;

            if (document.Status != InventoryDocumentStatus.CONFIRMED)
            {
                return null;
            }

            var snapshot = await _snapshotService.GetSnapshotAsync(dto.DocumentId);

            if (snapshot == null || snapshot.Status != InventoryDocumentStatus.CONFIRMED)
            {
                return null;
            }

            return dto.ExportType switch
            {
                InventoryDocumentExportType.PDF => await _exportService.ExportPdfAsync(snapshot),

                InventoryDocumentExportType.WORD => await _exportService.ExportWordAsync(snapshot),

                _ => null
            };
        }

        public async Task<byte[]> ExportExcelAsync(
            AdminInventoryDocumentFilterDTO filter)
        {
            var allowedStoreIds = await GetAllowedStoreIdsAsync();
            var documents =
                await BuildFilteredDocumentsQuery(filter)
                    .Where(x => allowedStoreIds.Contains(x.StoreId))
                    .OrderByDescending(x => x.DocumentDate)
                    .ThenByDescending(x => x.InventoryDocumentId)
                    .Select(x =>
                        new
                        {
                            Document =
                                new AdminInventoryDocumentExcelRowDTO
                                {
                                    Code = x.Code,

                                    Type = x.Type,

                                    Purpose = x.Purpose,

                                    StoreName = x.Store.Name,

                                    PartnerName = x.PartnerName,

                                    DocumentDate = x.DocumentDate,

                                    FinalAmount = x.FinalAmount ?? 0,

                                    Status = x.Status,

                                    ConfirmedAt = x.ConfirmedAt
                                },

                            Details =
                                x.Details
                                    .OrderBy(detail => detail.InventoryDocumentDetailId)
                                    .Select(detail =>
                                        new AdminInventoryDocumentExcelDetailRowDTO
                                        {
                                            DocumentCode = x.Code,

                                            Type = x.Type,

                                            Purpose = x.Purpose,

                                            StoreName = x.Store.Name,

                                            DocumentDate = x.DocumentDate,

                                            Status = x.Status,

                                            IngredientName = detail.Ingredient.Name,

                                            UnitName = detail.Unit.Name,

                                            Quantity = detail.Quantity,

                                            BaseQuantity = detail.BaseQuantity,

                                            UnitPrice = detail.UnitPrice ?? 0,

                                            TotalAmount = detail.TotalAmount ?? 0,

                                            CostPrice = detail.CostPrice ?? 0,

                                            CostAmount = detail.CostAmount ?? 0,

                                            Note = detail.Note ?? x.Note
                                        })
                                    .ToList()
                        })
                    .ToListAsync();

            var rows =
                documents
                    .Select(x => x.Document)
                    .ToList();

            var detailRows =
                documents
                    .SelectMany(x => x.Details)
                    .ToList();

            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].No =
                    i + 1;
            }

            for (var i = 0; i < detailRows.Count; i++)
            {
                detailRows[i].No =
                    i + 1;
            }

            return await _exportService.ExportExcelAsync(
                new AdminInventoryDocumentExcelExportDTO
                {
                    Documents = rows,
                    Details = detailRows
                });
        }

        private async Task<List<int>> GetAllowedStoreIdsAsync()
        {
            var actor = GetActor();
            if (actor.StaffId <= 0)
                return [];

            return (await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId))
                .Select(x => x.StoreId)
                .Distinct()
                .ToList();
        }

        private async Task<bool> CanAccessStoreAsync(int storeId)
        {
            var actor = GetActor();
            return actor.StaffId > 0
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }

        private CafeChain.Application.DTOs.Admin.Actor.AdminActorContext GetActor()
        {
            var principal = _httpContextAccessor.HttpContext?.User
                ?? new System.Security.Claims.ClaimsPrincipal();
            return _actorAccessor.Get(principal);
        }

        private static bool CanApproveNegative(IReadOnlyList<string> roles)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RoleConstants.BusinessOwner,
                RoleConstants.SystemAdmin,
                RoleConstants.AreaManager,
                RoleConstants.AccountantWarehouse
            };

            return roles.Any(allowed.Contains);
        }

        private static string? GetNegativeApprovalReviewMessage(
            string? approvalStatus,
            InventoryDocumentStatus documentStatus,
            int? requesterStaffId,
            int actorStaffId,
            IReadOnlyList<string> roles)
        {
            if (approvalStatus == null)
            {
                return null;
            }

            if (approvalStatus != InventoryNegativeApprovalStatuses.Requested)
            {
                return approvalStatus switch
                {
                    InventoryNegativeApprovalStatuses.Approved => "Yêu cầu xuất âm đã được duyệt.",
                    InventoryNegativeApprovalStatuses.Rejected => "Yêu cầu xuất âm đã bị từ chối.",
                    InventoryNegativeApprovalStatuses.Cancelled => "Yêu cầu xuất âm đã bị hủy.",
                    _ => "Yêu cầu xuất âm không còn chờ duyệt."
                };
            }

            if (documentStatus != InventoryDocumentStatus.PENDING)
            {
                return "Phiếu không còn ở trạng thái chờ duyệt.";
            }

            if (requesterStaffId == actorStaffId)
            {
                return "Bạn không thể tự duyệt phiếu do chính mình tạo. Hãy dùng một tài khoản người duyệt khác có quyền tại cửa hàng này.";
            }

            if (!CanApproveNegative(roles))
            {
                return "Tài khoản hiện tại không có quyền duyệt xuất âm. Vai trò được phép: BusinessOwner, SystemAdmin, AreaManager hoặc AccountantWarehouse.";
            }

            return null;
        }

    }
}
