# Module 3 Guide: Biometric Face-Scanning & Core Timekeeping

This guide details the specifications, front-to-back APIs, database schemas, and background job rules for the Biometric Timekeeping and Shift calculations.

---

## 1. Objectives & Business Logic
1. **Biometric Face Registration**: Capture 3 angle scans (Straight, Left, Right) in the browser, calculate the 128-dimensional coordinate floating vector, compute the average vector, and save it as a JSON string inside the `Staff.FaceDescriptor` table.
2. **Biometric Verification**: During check-in, verify the live-scanned face descriptor against the registered database vector using **Cosine Similarity** (threshold $\le 0.4$ distance represents a match).
3. **Smart Shift Association**: Automatic allocation of timekeeping actions to the correct scheduled `StaffShiftId`.
4. **Ad-Hoc Shift Management (`IsFreeShift`)**: If a shift is flexible or the employee is working an ad-hoc ca tự do, the system prompts confirmation via SweetAlert2, and inserts a `StaffShift` with `IsAdHoc = true`.
5. **Auto Payroll Rounding**: A background process reads completed `StaffShifts`, calculates actual hours worked, and updates `PayrollHours` rounding to the nearest 15 minutes.

---

## 2. Technical Implementation Architecture

### A. Cosine Similarity Vector Verification
The backend parses face descriptors (represented as arrays of 128 floats) and calculates Euclidean or Cosine distance to verify identities:

```csharp
public class AttendanceActionService : IAttendanceActionService
{
    private const double MatchThreshold = 0.4; // 3D Face Similarity Boundary

    public async Task<ServiceResult> SubmitTimeActionAsync(int accountId, string actionType, string faceDescriptorJson, bool forceSave = false)
    {
        var staff = await _context.Staffs
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.AccountId == accountId);

        if (staff == null) return ServiceResult.Failure("Không tìm thấy hồ sơ nhân sự.");
        if (string.IsNullOrEmpty(staff.FaceDescriptor)) return ServiceResult.Failure("Tài khoản chưa đăng ký khuôn mặt Face ID.");

        // Parse vectors
        var inputVector = JsonConvert.DeserializeObject<float[]>(faceDescriptorJson);
        var dbVector = JsonConvert.DeserializeObject<float[]>(staff.FaceDescriptor);

        double distance = CalculateFaceDistance(inputVector, dbVector);
        if (distance > MatchThreshold)
        {
            return ServiceResult.Failure("Xác thực thất bại! Khuôn mặt không trùng khớp.");
        }

        // Proceed to process Shift Mapping ...
        return await ProcessShiftAssociationAsync(staff, actionType, forceSave);
    }

    private double CalculateFaceDistance(float[] vec1, float[] vec2)
    {
        // Cosine / Euclidean distance calculation
        double sum = 0.0;
        for (int i = 0; i < 128; i++)
        {
            double diff = vec1[i] - vec2[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
}
```

### B. Shift Mapping & Overtime Calculations
1. **Regular Shift Check**: Find an upcoming `StaffShift` starting within a 2-hour window of the check-in attempt.
2. **Ad-Hoc Check-In Warning**: If no shift is found and `forceSave = false`, return a distinct validation code:
   - `errorCode: "AD_HOC_CONFIRMATION_REQUIRED"`
   - Prompt SweetAlert2 to confirm. If confirmed, call API again with `forceSave = true` to insert an ad-hoc record.
3. **Overnight Shift (`IsOvernight = true`)**: If the assigned shift is overnight, cross the calendar day barrier gracefully by appending 24 hours to the calculated threshold.

### C. Background Work Hours Calculation Job
A periodic cron background worker processes completed shifts, filters anomalies, and calculates decimal-based payroll hours rounded to 15-minute segments:

```csharp
public async Task CalculatePayrollHoursAsync(int staffShiftId)
{
    var shift = await _context.StaffShifts
        .Include(s => s.Shift)
        .FirstOrDefaultAsync(s => s.Id == staffShiftId);

    if (shift == null || shift.ActualCheckIn == null || shift.ActualCheckOut == null) return;

    var actualDuration = shift.ActualCheckOut.Value - shift.ActualCheckIn.Value;
    double rawHours = actualDuration.TotalHours;

    // Rounding logic to 15 minutes (0.25h)
    double roundedHours = Math.Round(rawHours * 4, MidpointRounding.ToEven) / 4;

    shift.PayrollHours = (decimal)roundedHours;
    shift.StatusId = (int)ShiftStatus.Completed;
    await _context.SaveChangesAsync();
}
```
