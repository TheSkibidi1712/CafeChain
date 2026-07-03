export const TYPE = {
    IMPORT: 1,
    EXPORT: 2,
    WASTE: 3,
    STOCK_TAKE: 4,
    ADJUSTMENT_IN: 8,
    INTERNAL_IMPORT: 9
};

export const PURPOSE = {
    NONE: 0,

    // IMPORT
    IMPORT_PURCHASE: 1,
    IMPORT_INTERNAL: 2,
    IMPORT_ADJUSTMENT: 3,

    // EXPORT
    SALE: 5,
    INTERNAL_OUT: 6,
    GIFT: 7,
    DEBT: 8,
    SAMPLE: 9,
    ADJUSTMENT_OUT: 10,

    // STOCK TAKE
    STOCK_TAKE: 11,

    // WASTE
    DAMAGED: 12,
    EXPIRED: 13,
    BROKEN: 14,
    CONTAMINATED: 15,
    LOST: 16
};
