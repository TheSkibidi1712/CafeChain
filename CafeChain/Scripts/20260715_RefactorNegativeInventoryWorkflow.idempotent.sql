/*
    DEPRECATED / DO NOT RUN

    Script name retained only as an execution guard for references to the old
    migration chain. The current fresh-database baseline is:

        20260715104817_InitialCreate

    This file intentionally contains no schema mutation. A database that has
    migration history from the previous chain requires backup, audit and a
    purpose-built reconcile migration. Do not apply the new InitialCreate over it.
*/

THROW 51000, N'DEPRECATED_MIGRATION_CHAIN: Không chạy script này. Xem CafeChain/FIX.md và NegativeInventoryAcceptance.md.', 1;
