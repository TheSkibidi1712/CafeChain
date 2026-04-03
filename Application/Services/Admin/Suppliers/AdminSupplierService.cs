using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Infrastrusture.Interfaces.Admin.Suppliers;
using CafeChain.Models.Inventories;

namespace CafeChain.Application.Services.Admin.Suppliers
{
    public class AdminSupplierService : IAdminSupplierService
    {
        private readonly IAdminSupplierRepository _supplierRepository;

        public AdminSupplierService(IAdminSupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;
        }

        public async Task<IEnumerable<AdminSupplierDTO>> GetAllSuppliersAsync()
        {
            var suppliers = await _supplierRepository.GetAllSuppliersAsync();
            return suppliers.Select(s => new AdminSupplierDTO
            {
                SupplierId  = s.SupplierId,
                Code        = s.Code,
                Name        = s.Name,
                Phone       = s.Phone,
                Address     = s.Address,
                DebtAmount  = s.DebtAmount,
                Active      = s.Active
            });
        }

        public async Task<AdminSupplierUpdateDTO> GetSupplierForUpdateAsync(int id)
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null) return null;

            return new AdminSupplierUpdateDTO
            {
                SupplierId = supplier.SupplierId,
                Code       = supplier.Code,
                Name       = supplier.Name,
                Phone      = supplier.Phone,
                Address    = supplier.Address,
                Active     = supplier.Active
            };
        }

        public async Task CreateSupplierAsync(AdminSupplierCreateDTO dto)
        {
            if (await _supplierRepository.IsSupplierCodeExistsAsync(dto.Code))
                throw new ArgumentException("Mã nhà cung cấp đã tồn tại.");

            if (await _supplierRepository.IsSupplierNameExistsAsync(dto.Name))
                throw new ArgumentException("Tên nhà cung cấp đã tồn tại.");

            if (!string.IsNullOrWhiteSpace(dto.Phone) && await _supplierRepository.IsSupplierPhoneExistsAsync(dto.Phone))
                throw new ArgumentException("Số điện thoại này đã được dùng bởi nhà cung cấp khác.");

            var supplier = new Supplier
            {
                Code       = dto.Code.Trim().ToUpper(),
                Name       = dto.Name.Trim(),
                Phone      = dto.Phone?.Trim(),
                Address    = dto.Address?.Trim(),
                DebtAmount = 0,
                Active     = true
            };

            await _supplierRepository.CreateSupplierAsync(supplier);
        }

        public async Task UpdateSupplierAsync(AdminSupplierUpdateDTO dto)
        {
            if (await _supplierRepository.IsSupplierCodeExistsAsync(dto.Code, dto.SupplierId))
                throw new ArgumentException("Mã nhà cung cấp đã tồn tại.");

            if (await _supplierRepository.IsSupplierNameExistsAsync(dto.Name, dto.SupplierId))
                throw new ArgumentException("Tên nhà cung cấp đã tồn tại.");

            if (!string.IsNullOrWhiteSpace(dto.Phone) && await _supplierRepository.IsSupplierPhoneExistsAsync(dto.Phone, dto.SupplierId))
                throw new ArgumentException("Số điện thoại này đã được dùng bởi nhà cung cấp khác.");

            var supplier = await _supplierRepository.GetSupplierByIdAsync(dto.SupplierId);
            if (supplier == null) throw new KeyNotFoundException("Không tìm thấy nhà cung cấp.");

            supplier.Code    = dto.Code.Trim().ToUpper();
            supplier.Name    = dto.Name.Trim();
            supplier.Phone   = dto.Phone?.Trim();
            supplier.Address = dto.Address?.Trim();
            supplier.Active  = dto.Active;

            await _supplierRepository.UpdateSupplierAsync(supplier);
        }

        public async Task ToggleSupplierStatusAsync(int id)
        {
            await _supplierRepository.ToggleSupplierStatusAsync(id);
        }

        public async Task AdjustDebtAsync(int id, decimal amount)
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null) throw new KeyNotFoundException("Không tìm thấy nhà cung cấp.");
            await _supplierRepository.AdjustDebtAsync(id, amount);
        }
    }
}
