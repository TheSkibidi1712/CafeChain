export const TYPE = {
    IMPORT: 1,
    EXPORT: 2,
    WASTE: 3,
    STOCK_TAKE: 4,
    ADJUSTMENT_IN: 8
};

export const PURPOSE = {
    NONE: 0,

    // IMPORT
    IMPORT_PURCHASE: 1,

    // EXPORT
    SALE: 5,

    // STOCK TAKE
    STOCK_TAKE: 11,

    // WASTE
    DAMAGED: 12,
    EXPIRED: 13,
    BROKEN: 14,
    CONTAMINATED: 15,
    LOST: 16
};
