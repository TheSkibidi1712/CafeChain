using CafeChain.Application.Constants.Cloudinaries;
using CafeChain.Application.DTOs.Customer;
using CafeChain.Application.DTOs.Customers;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Cloudinaries;
using CafeChain.Application.Interfaces.Customers;
using CafeChain.Data; // Chỉnh lại theo tên DbContext của bác
using CafeChain.Helpers.Cloudinaries;
using CafeChain.Infrastructure.Interfaces.Customers;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Cloudinaries;
using CafeChain.Models.Enums.Customer;
using CafeChain.Models.Locations;
using CafeChain.Models.Loyalties;
using CafeChain.ViewModels.Customers;
using Castle.Core.Resource;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
namespace CafeChain.Application.Services.Customers
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IGeocodingService _geocodingService;
        private readonly ICloudinaryService _cloudinaryService;

        public CustomerService(ICustomerRepository customerRepository, ICloudinaryService cloudinaryService, IGeocodingService geocodingService)
        {
            _customerRepository = customerRepository;
            _cloudinaryService = cloudinaryService;
            _geocodingService = geocodingService;
        }

        // =========================
        // CUSTOMER PROFILE
        // =========================
        public async Task<CustomerProfileViewModel?> GetCustomerProfileAsync(string accountId)
        {
            if (!int.TryParse(accountId, out int accId))
            {
                return null;
            }

            var account = await _customerRepository.GetCustomerProfileAccountAsync(accId);

            if (account?.Customer == null)
            {
                return null;
            }

            var customer = account.Customer;

            int totalPoints = customer.CurrentPoints;

            var currentTier = customer.MemberLevel;

            string currentTierName = currentTier?.Name ?? "Thành viên mới";

            string nextTierName = string.Empty;
            int pointsNeeded = 0;
            double progressPercentage = 100;

            if (currentTier != null)
            {
                pointsNeeded = 0;
            }

            return new CustomerProfileViewModel
            {
                Customer = customer,
                Email = account.Email,
                TotalPoints = totalPoints,
                CurrentTierName = currentTierName,
                NextTierName = nextTierName,
                PointsNeeded = pointsNeeded,
                ProgressPercentage = progressPercentage
            };
        }

        // ====================================================
        // UPDATE AVATAR & PROFILE
        // ====================================================
        public async Task<string> UpdateAvatarAsync(int customerId, IFormFile file)
        {
            // ==========================================
            // VALIDATE FILE
            // ==========================================
            ImageValidationHelper.Validate(
                file,
                ImageCategory.Avatar);

            // ==========================================
            // GET CUSTOMER
            // ==========================================
            var customer =
                await _customerRepository
                    .GetByIdAsync(customerId);

            if (customer == null)
            {
                throw new KeyNotFoundException(
                    "Không tìm thấy khách hàng.");
            }

            // ==========================================
            // UPLOAD NEW IMAGE
            // ==========================================
            var uploadResult =
                await _cloudinaryService.UploadAsync(
                    file,
                    ImageFolder.Customers,
                    ImageCategory.Avatar);

            // ==========================================
            // UPDATE DATABASE
            // ==========================================
            await _customerRepository.UpdateAvatarAsync(
                customer,
                uploadResult.Url,
                uploadResult.PublicId);

            await _customerRepository.SaveChangesAsync();

            return uploadResult.Url;
        }


        public async Task<bool> UpdateProfileAsync(int customerId, UpdateProfileRequest request)
        {
            var customer = await _customerRepository.GetCustomerForUpdateAsync(customerId);

            if (customer == null)
            {
                throw new KeyNotFoundException(
                    "Không tìm thấy khách hàng.");
            }

            // ==========================================
            // BUSINESS VALIDATION
            // ==========================================

            ValidateDateOfBirth(request);

            ValidatePrimaryPhone(customer, request);

            ValidatePrimaryAddress(customer, request);

            ValidateNewPhones(customer, request);

            ValidateAddresses(request);

            // ==========================================
            // UPDATE DATA
            // ==========================================

            UpdateBasicInfo(customer, request);

            AddNewPhones(customer, request);

            await UpdateAddressesAsync(customer, request);

            var newlyAddedAddresses = await AddNewAddressesAsync(customer, request);

            SetPrimaryPhone(customer, request.PrimaryPhone);

            SetPrimaryAddress(customer, request.PrimaryAddressId, newlyAddedAddresses);

            await _customerRepository.SaveChangesAsync();

            return true;
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int accountId, ChangePasswordRequest request)
        {
            // =========================
            // VALIDATE
            // =========================

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return (false, "Vui lòng nhập mật khẩu hiện tại.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return (false, "Vui lòng nhập mật khẩu mới.");
            }

            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                return (false, "Vui lòng xác nhận mật khẩu.");
            }

            // =========================
            // GET ACCOUNT
            // =========================

            var account =
                await _customerRepository
                    .GetAccountByIdAsync(accountId);

            if (account == null)
            {
                return (
                    false,
                    "Không tìm thấy tài khoản."
                );
            }

            // =========================
            // CHECK CURRENT PASSWORD
            // =========================

            bool isCurrentPasswordValid =BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash);

            if (!isCurrentPasswordValid)
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            // =========================
            // CHECK SAME PASSWORD
            // =========================

            if (request.CurrentPassword == request.NewPassword)
            {
                return (false, "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
            }

            // =========================
            // CHECK CONFIRM PASSWORD
            // =========================

            if (request.NewPassword != request.ConfirmPassword)
            {
                return (false, "Mật khẩu xác nhận không khớp.");
            }

            // =========================
            // UPDATE PASSWORD
            // =========================

            account.PasswordHash =BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            await _customerRepository.UpdateAccountAsync(account);

            await _customerRepository.SaveChangesAsync();

            return (true, "Đổi mật khẩu thành công!");
        }

        // =========================
        // OTHER CUSTOMER METHODS
        // =========================
        public async Task<Customer> GetByPhoneAsync(string phone)
        {
            return await _customerRepository.GetByPhoneAsync(phone);
        }


        // =========================
        // QUICK REGISTER (DÙNG CHO POS)
        // =========================
        public async Task<(bool Success, string Message, int CustomerId)> QuickRegisterAsync(string fullName, string phone)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (false, "Tên khách hàng không hợp lệ.", 0);
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                return ( false, "Số điện thoại không hợp lệ.", 0);
            }

            var exists = await _customerRepository.PhoneExistsAsync(phone);

            if (exists)
            {
                return ( false, "Số điện thoại này đã được sử dụng.", 0);
            }

            int customerId = 0;

            await _customerRepository.ExecuteInTransactionAsync(
                async () =>
                {
                    var account = new Account
                    {
                        Email = $"pos_{phone}@cafechain.com",

                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),

                        Active = true,

                        CreatedAt = DateTime.UtcNow
                    };

                    await _customerRepository
                        .AddAccountAsync(account);

                    await _customerRepository
                        .SaveChangesAsync();

                    var customer = new Customer
                    {
                        AccountId = account.AccountId,

                        CustomerCode = $"CUS{DateTime.UtcNow.Ticks}",

                        FullName = fullName,

                        Category = CustomerCategory.Registered,

                        CurrentPoints = 0,

                        TotalSpent = 0,

                        TotalOrders = 0,

                        Active = true,

                        IsDeleted = false,

                        CreatedAt = DateTime.UtcNow
                    };

                    await _customerRepository
                        .AddCustomerAsync(customer);

                    await _customerRepository
                        .SaveChangesAsync();

                    await _customerRepository.AddCustomerPhoneAsync(
                            new CustomerPhone
                            {
                                CustomerId =
                                    customer.CustomerId,

                                Phone = phone,

                                IsDefault = true
                            });

                    customerId = customer.CustomerId;
                });

            return (true, "Đăng ký thành viên thành công!", customerId);
        }


        // ======================= LOCATION METHODS =========================
        public async Task<List<Province>> GetProvincesAsync()
        {
            return await _customerRepository
                .GetProvincesAsync();
        }

        public async Task<List<District>> GetDistrictsByProvinceAsync(int provinceId)
        {
            if (provinceId <= 0)
            {
                throw new ArgumentException("Tỉnh/Thành phố không hợp lệ.");
            }

            return await _customerRepository.GetDistrictsByProvinceAsync(provinceId);
        }

        public async Task<List<Ward>> GetWardsByDistrictAsync(int districtId)
        {
            if (districtId <= 0)
            {
                throw new ArgumentException("Quận/Huyện không hợp lệ.");
            }

            return await _customerRepository.GetWardsByDistrictAsync(districtId);
        }


        // =========================
        // PRIVATE HELPER METHODS
        // =========================
        private static void UpdateBasicInfo(Customer customer, UpdateProfileRequest request)
        {
            customer.FullName = request.FullName?.Trim();

            customer.DateOfBirth = request.Dob;
        }

        private static void AddNewPhones(Customer customer, UpdateProfileRequest request)
        {
            if (request.NewPhones == null || !request.NewPhones.Any())
            {
                return;
            }

            foreach (var phone in request.NewPhones)
            {
                if (customer.CustomerPhones.Any(x => x.Phone == phone))
                {
                    continue;
                }

                customer.CustomerPhones.Add(
                    new CustomerPhone
                    {
                        Phone = phone
                    });
            }
        }

        private async Task UpdateAddressesAsync(Customer customer, UpdateProfileRequest request)
        {
            if (request.UpdatedAddresses == null || !request.UpdatedAddresses.Any())
            {
                return;
            }

            foreach (var dto in request.UpdatedAddresses)
            {
                var address = customer.CustomerAddresses.FirstOrDefault(x => x.CustomerAddressId == dto.CustomerAddressId);

                if (address == null)
                {
                    continue;
                }

                address.Address = dto.Street;

                address.WardId = dto.WardId;

                address.DistrictId = dto.DistrictId;

                address.ProvinceId = dto.ProvinceId;

                await UpdateCoordinatesAsync(address, dto.Street, dto.WardId, dto.DistrictId, dto.ProvinceId);
            }
        }

        private async Task<List<(CustomerAddress Entity, int TempId)>> AddNewAddressesAsync(Customer customer, UpdateProfileRequest request)
        {
            var result = new List<(CustomerAddress, int)>();

            if (request.NewAddresses == null || !request.NewAddresses.Any())
            {
                return result;
            }

            foreach (var dto in request.NewAddresses)
            {
                var address = new CustomerAddress
                    {
                        Address = dto.Street,

                        WardId = dto.WardId,

                        DistrictId = dto.DistrictId,

                        ProvinceId = dto.ProvinceId
                    };

                await UpdateCoordinatesAsync(address, dto.Street, dto.WardId, dto.DistrictId, dto.ProvinceId);

                customer.CustomerAddresses.Add(address);

                result.Add((address, dto.TempId));
            }

            return result;
        }

        private async Task UpdateCoordinatesAsync(CustomerAddress address, string street, int wardId, int districtId, int provinceId)
        {
            var location = await _customerRepository.GetLocationNamesAsync(provinceId, districtId, wardId);

            if (location == null)
            {
                return;
            }

            var fullAddress = $"{street}, " + $"{location.WardName}, " + $"{location.DistrictName}, " + $"{location.ProvinceName}";

            var (lat, lng) = await _geocodingService.GetCoordinatesAsync(fullAddress);

            if (lat != null && lng != null)
            {
                address.Latitude = lat;

                address.Longitude = lng;
            }
        }

        private static void SetPrimaryPhone(Customer customer, string? primaryPhone)
        {
            if (string.IsNullOrWhiteSpace(primaryPhone))
            {
                return;
            }

            foreach (var phone in customer.CustomerPhones)
            {
                phone.IsDefault = phone.Phone == primaryPhone;
            }
        }

        private static void SetPrimaryAddress(Customer customer, int? primaryAddressId, List<(CustomerAddress Entity, int TempId)> newAddresses)
        {
            if (!primaryAddressId.HasValue)
            {
                return;
            }

            int id = primaryAddressId.Value;

            if (id < 0)
            {
                foreach (var address in customer.CustomerAddresses)
                {
                    address.IsDefault = false;
                }

                var target = newAddresses.FirstOrDefault(x => x.TempId == id).Entity;

                if (target != null)
                {
                    target.IsDefault = true;
                }

                return;
            }

            foreach (var address in customer.CustomerAddresses)
            {
                address.IsDefault = address.CustomerAddressId == id;
            }
        }

        private static void ValidateDateOfBirth(UpdateProfileRequest request)
        {
            if (!request.Dob.HasValue)
            {
                return;
            }

            if (request.Dob.Value.Date > DateTime.Today)
            {
                throw new ArgumentException(
                    "Ngày sinh không hợp lệ.");
            }
        }

        private static void ValidatePrimaryPhone(Customer customer, UpdateProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PrimaryPhone))
            {
                return;
            }

            bool exists = customer.CustomerPhones.Any(x => x.Phone == request.PrimaryPhone);

            bool willBeAdded = request.NewPhones.Any(x => x == request.PrimaryPhone);

            if (!exists && !willBeAdded)
            {
                throw new ArgumentException("Số điện thoại mặc định không tồn tại.");
            }
        }

        private static void ValidatePrimaryAddress(Customer customer, UpdateProfileRequest request)
        {
            if (!request.PrimaryAddressId.HasValue)
            {
                return;
            }

            int addressId = request.PrimaryAddressId.Value;

            // địa chỉ mới dùng TempId âm
            if (addressId < 0)
            {
                bool exists = request.NewAddresses.Any(x => x.TempId == addressId);

                if (!exists)
                {
                    throw new ArgumentException(
                        "Địa chỉ mặc định không hợp lệ.");
                }

                return;
            }

            bool addressExists = customer.CustomerAddresses.Any(x => x.CustomerAddressId == addressId);

            if (!addressExists)
            {
                throw new ArgumentException("Địa chỉ mặc định không tồn tại.");
            }
        }

        private static void ValidateNewPhones(Customer customer, UpdateProfileRequest request)
        {
            if (!request.NewPhones.Any())
            {
                return;
            }

            var duplicatedPhones = request.NewPhones.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key).ToList();

            if (duplicatedPhones.Any())
            {
                throw new ArgumentException("Danh sách số điện thoại bị trùng.");
            }

            foreach (var phone in request.NewPhones)
            {
                if (customer.CustomerPhones.Any(x => x.Phone == phone))
                {
                    throw new ArgumentException($"Số điện thoại {phone} đã tồn tại.");
                }
            }
        }

        private static void ValidateAddresses(UpdateProfileRequest request)
        {
            foreach (var address in request.NewAddresses)
            {
                if (string.IsNullOrWhiteSpace( address.Street))
                {
                    throw new ArgumentException("Địa chỉ mới không hợp lệ.");
                }
            }

            foreach (var address in request.UpdatedAddresses)
            {
                if (string.IsNullOrWhiteSpace( address.Street))
                {
                    throw new ArgumentException("Địa chỉ cập nhật không hợp lệ.");
                }
            }
        }
    }
}