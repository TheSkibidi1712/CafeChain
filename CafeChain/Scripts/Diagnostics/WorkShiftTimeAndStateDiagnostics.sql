/* Read-only diagnostics for POS WorkShift UTC/active-state incidents. */
SET NOCOUNT ON;

SELECT TOP (200)
    ShiftId, StoreId, UserId, PosTerminalId, Status, OpenContext,
    StartTimeUtc, AutoCloseAtUtc, ExpiredAtUtc, EndTimeUtc,
    DATEDIFF(SECOND, StartTimeUtc, AutoCloseAtUtc) AS OutsideDurationSeconds,
    StartingCash, ExpectedEndingCash, ActualEndingCash, CloseType
FROM dbo.WorkShifts
ORDER BY ShiftId DESC;

SELECT UserId, COUNT(*) AS ActiveCount, STRING_AGG(CONVERT(varchar(20), ShiftId), ',') AS ShiftIds
FROM dbo.WorkShifts
WHERE Status IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')
GROUP BY UserId
HAVING COUNT(*) > 1;

SELECT PosTerminalId, COUNT(*) AS ActiveCount, STRING_AGG(CONVERT(varchar(20), ShiftId), ',') AS ShiftIds
FROM dbo.WorkShifts
WHERE PosTerminalId IS NOT NULL
  AND Status IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE')
GROUP BY PosTerminalId
HAVING COUNT(*) > 1;

SELECT ShiftId, Status, EndTimeUtc, ActualEndingCash, CloseType, RequiresReconciliation
FROM dbo.WorkShifts
WHERE (Status IN ('OPEN','CLOSING','EXPIRED_PENDING_CLOSE') AND EndTimeUtc IS NOT NULL)
   OR (Status = 'CLOSED' AND EndTimeUtc IS NULL)
   OR (Status = 'EXPIRED_PENDING_CLOSE' AND AutoCloseAtUtc IS NULL)
ORDER BY ShiftId DESC;
