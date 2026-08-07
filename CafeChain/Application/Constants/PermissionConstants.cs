namespace CafeChain.Application.Constants
{
    public static partial class PermissionConstants
    {
        public const string CategoryCreate = "Category.Create";
        public const string CategoryDelete = "Category.Delete";
        public const string CategoryToggleStatus = "Category.ToggleStatus";
        public const string CategoryUpdate = "Category.Update";
        public const string CategoryView = "Category.View";

        public const string DrinkView = "Drink.View";
        public const string DrinkCreate = "Drink.Create";
        public const string DrinkUpdate = "Drink.Update";
        public const string DrinkToggleStatus = "Drink.ToggleStatus";
        public const string DrinkUpdateImage = "Drink.UpdateImage";

        public const string SizeView = "Size.View";
        public const string SizeCreate = "Size.Create";
        public const string SizeUpdate = "Size.Update";
        public const string SizeToggleStatus = "Size.ToggleStatus";
        public const string SizeAssignDrink = "Size.AssignDrink";

        public const string ToppingView = "Topping.View";
        public const string ToppingCreate = "Topping.Create";
        public const string ToppingUpdate = "Topping.Update";
        public const string ToppingToggleStatus = "Topping.ToggleStatus";
        public const string ToppingAssignDrink = "Topping.AssignDrink";

        public const string SystemPermissionManage = "System.Permission.Manage";

        public const string AppAdminDashboard = "App.AdminDashboard";
        public const string AppStaffHub = "App.StaffHub";
        public const string AppPos = "App.POS";
        public const string PosWorkShiftView = "POS.WorkShift.View";
        public const string PosWorkShiftOpen = "POS.WorkShift.Open";
        public const string PosWorkShiftClose = "POS.WorkShift.Close";
        public const string PosWorkShiftOpenOutsideSchedule = "POS.WorkShift.OpenOutsideSchedule";
        public const string PosWorkShiftApproveOutsideSchedule = "POS.WorkShift.ApproveOutsideSchedule";
        public const string PosWorkShiftCloseException = "POS.WorkShift.CloseException";
        public const string PosWorkShiftReconcile = "POS.WorkShift.Reconcile";
        public const string PosWorkShiftOverrideTerminal = "POS.WorkShift.OverrideTerminal";
        public const string PosWorkShiftApproveLateOpen = "POS.WorkShift.ApproveLateOpen";
        public const string PosSessionManage = "POS.Session.Manage";
        public const string PosOperatorSwitch = "POS.Operator.Switch";
        public const string NotificationView = "Notification.View";

        public const string IngredientView = "Ingredient.View";
        public const string IngredientCreate = "Ingredient.Create";
        public const string IngredientUpdate = "Ingredient.Update";
        public const string IngredientToggleStatus = "Ingredient.ToggleStatus";

        public const string StaffView = "Staff.View";
        public const string StaffCreate = "Staff.Create";
        public const string StaffUpdate = "Staff.Update";
        public const string StaffToggleStatus = "Staff.ToggleStatus";
        public const string StaffResetPassword = "Staff.ResetPassword";

        public const string ShiftView = "Shift.View";
        public const string ShiftCreate = "Shift.Create";
        public const string ShiftUpdate = "Shift.Update";
        public const string ShiftCancel = "Shift.Cancel";

        public const string StoreView = "Store.View";
        public const string StoreCreate = "Store.Create";
        public const string StoreUpdate = "Store.Update";
        public const string StoreToggleStatus = "Store.ToggleStatus";

        public const string SettingsView = "Settings.View";
        public const string SettingsUpdate = "Settings.Update";

        public const string OperationalIceView = "OperationalIce.View";
        public const string OperationalIceConfigurePolicy = "OperationalIce.ConfigurePolicy";
        public const string OperationalIceCreateShift = "OperationalIce.CreateShift";
        public const string OperationalIceOpenShift = "OperationalIce.OpenShift";
        public const string OperationalIceLinkWorkShift = "OperationalIce.LinkWorkShift";
        public const string OperationalIceRequestSupplement = "OperationalIce.RequestSupplement";
        public const string OperationalIceApproveSupplement = "OperationalIce.ApproveSupplement";
        public const string OperationalIceHandoff = "OperationalIce.Handoff";
        public const string OperationalIceSubmitClose = "OperationalIce.SubmitClose";
        public const string OperationalIceApproveVariance = "OperationalIce.ApproveVariance";
        public const string OperationalIceCancelScheduledShift = "OperationalIce.CancelScheduledShift";
        public const string OperationalIceViewReport = "OperationalIce.ViewReport";
        public const string OperationalIceLegacyManage = "OperationalIce.Manage";
        public const string OperationalIceLegacyApprove = "OperationalIce.Approve";
        public const string OperationalIceLegacyPolicy = "OperationalIce.Policy";

        // Inventory replenishment permissions already present in SeedAll.sql.
        public const string ReorderSuggestionView = "ReorderSuggestion.View";
        public const string RestockView = "Restock.View";
        public const string RestockCreate = "Restock.Create";
    }
}
