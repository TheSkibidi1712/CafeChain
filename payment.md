# ROLE & CONTEXT
Act as a Senior ASP.NET Core MVC Backend Engineer. Your task is to implement the Checkout, Payment, and Order modules for an F&B system. 
CRITICAL: Another developer is currently working on the Inventory module (`StoreInventory` entity). To avoid Git merge conflicts, you MUST NOT directly query or update any inventory tables in the `DbContext`. Instead, use Dependency Injection with an Interface.

# BUSINESS REQUIREMENTS

## 1. Constants (Do NOT use hardcoded Enums for DB fields)
Create `SystemConstants.cs`:
- `OrderStatuses`: Draft = 1, PendingApproval = 2, Approved = 3, Preparing = 4, WaitingForPickup = 5, Delivering = 6, Completed = 7, CancelledByUser = 8, CancelledBySystem = 9.
- `PaymentStatuses`: Unpaid = 1, Pending = 2, Paid = 3, Failed = 4, Refunded = 5.
- `OrderTypes`: DineIn = 1, TakeAway = 2, Delivery = 3.

## 2. Inventory Abstraction (Anti-Conflict Pattern)
Generate an interface `IInventoryService` with two methods:
- `Task<bool> ReserveInventoryForOrderAsync(int storeId, List<CartItemDto> items)`
- `Task ReleaseInventoryForOrderAsync(int orderId)`
Generate a dummy implementation `MockInventoryService` that just returns `true` (so the checkout flow can proceed without the real DB tables).

## 3. Mandatory Architectural Patterns
1. **Zero-Trust Pricing:** The `CheckoutRequestDto` only contains `StoreId`, `CustomerId`, `List<CartItemDto>`, and `VoucherCode`. The server MUST query the `Drinks` table to calculate the real price.
2. **Order Snapshotting:** Save `DrinkName` and `Price` into `OrderDetails` at the exact moment of checkout.
3. **Idempotency:** `CheckoutRequestDto` must contain a `CheckoutToken` (Guid). Use `IMemoryCache` to lock this token.

## 4. The Core Checkout Flow
Implement `CreateOrderAsync` inside an `IDbContextTransaction`:
- Check Idempotency token.
- Validate `Drinks` via DB.
- **Call `_inventoryService.ReserveInventoryForOrderAsync(...)`. If false, throw exception.**
- Calculate `SubTotal` and `Total` (handle Voucher logic).
- Save `Order` (`OrderStatusId = Draft` or `PendingApproval`).
- Save `OrderDetails` (Snapshot).
- Save `Payment` (`PaymentStatusId = Unpaid` or `Pending`).
- Commit Transaction.

## 5. Background Worker (Auto-Cancel Expired Orders)
- Create `OrderCleanupWorker` (IHostedService). Run every 5 mins.
- Find `Orders` where `PaymentStatus == Pending` and `CreatedAt < DateTime.Now.AddMinutes(-15)`.
- Update statuses to Cancelled/Failed.
- **CRITICAL: Call `_inventoryService.ReleaseInventoryForOrderAsync(orderId)` to free up ingredients.**

# EXPECTED OUTPUT
Generate production-ready C# code for:
1. `SystemConstants.cs`
2. `IInventoryService` and `MockInventoryService`.
3. `IOrderService` and its implementation.
4. `CheckoutController`.
5. `OrderCleanupWorker`.
Ensure all code compiles against standard EF Core.