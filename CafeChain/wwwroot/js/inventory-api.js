// inventory-api.js
export async function getAveragePrice(storeId, ingredientId) {
    try {
        const r = await fetch(
            `/api/admin/inventory-documents/average-price?storeId=${storeId}&ingredientId=${ingredientId}`
        );

        const res = await r.json();
        return res.averagePrice || 0;
    } catch {
        return 0;
    }
}

export async function getUnits(ingredientId) {
    try {
        const r = await fetch(
            `/api/admin/inventory-documents/units?ingredientId=${ingredientId}`
        );
        return await r.json();
    } catch {
        return [];
    }
}