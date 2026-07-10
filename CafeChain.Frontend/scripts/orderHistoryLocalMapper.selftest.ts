/**
 * Lightweight self-test for orderHistoryLocalMapper (no vitest/jest).
 *
 * Run from CafeChain.Frontend:
 *   npx tsx scripts/orderHistoryLocalMapper.selftest.ts
 */

import {
  formatHistoryMoney,
  mapLocalOrderSafe,
  mapLocalOrdersSafe,
  mapLocalPayments,
  pickMoney,
  resolveOrderTotal,
  ORDER_HISTORY_LABELS,
} from '../src/utils/orderHistoryLocalMapper'

let passed = 0
let failed = 0

function assert(condition: boolean, name: string, detail?: string): void {
  if (condition) {
    passed += 1
    console.log(`  OK  ${name}`)
  } else {
    failed += 1
    console.error(`  FAIL ${name}${detail ? ` — ${detail}` : ''}`)
  }
}

function assertEq<T>(actual: T, expected: T, name: string): void {
  const ok = Object.is(actual, expected) || JSON.stringify(actual) === JSON.stringify(expected)
  assert(ok, name, `expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`)
}

console.log('orderHistoryLocalMapper self-test\n')

// 1. Full local order maps normally
{
  const row = mapLocalOrderSafe({
    queueId: 1,
    clientOrderId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    storeId: 1,
    staffId: 2,
    workShiftId: 3,
    soldAt: '2026-07-10T10:00:00.000Z',
    orderType: 'dine-in',
    items: [{ menuItemId: 1, name: 'Cà phê', quantity: 1, unitPrice: 30000 }],
    cartSnapshot: [{ menuItemId: 1, name: 'Cà phê', sizeName: 'M', quantity: 1, unitPrice: 30000 }],
    paymentSnapshot: {
      method: 'cash',
      paymentMethodId: 1,
      amount: 30000,
      receivedAmount: 50000,
      changeAmount: 20000,
      capturedAt: '2026-07-10T10:00:00.000Z',
    },
    totalAmount: 30000,
    paymentMethod: 'cash',
    syncStatus: 'Pending',
    createdAt: Date.now(),
    retryCount: 0,
  })
  assertEq(row.total, 30000, '1 full order → order total')
  assertEq(row.payments[0]?.amount, 30000, '1 full order → payment line amount')
  assertEq(row.payments[0]?.receivedAmount, 50000, '1 full order → receivedAmount')
  assert(row.isDegraded !== true, '1 full order → not degraded')
  assert(row.items.length === 1 && row.items[0].drinkName === 'Cà phê', '1 full order → items')
}

// 2. paymentSnapshot undefined → no crash; total from totalAmount
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'legacy-no-snapshot',
    totalAmount: 45000,
    paymentMethod: 'cash',
    soldAt: '2026-07-01T08:00:00.000Z',
    syncStatus: 'Pending',
    cartSnapshot: [{ menuItemId: 2, name: 'Trà', quantity: 1, unitPrice: 45000 }],
    items: [],
  })
  assertEq(row.total, 45000, '2 no paymentSnapshot → total from totalAmount')
  assert(row.payments.length >= 1, '2 no paymentSnapshot → synthetic payment line')
  assertEq(row.payments[0]?.amount, 45000, '2 synthetic line amount = order total')
  assert(row.isDegraded === true, '2 marked degraded (missing snapshot)')
}

// 3. payments undefined → no crash
{
  const pays = mapLocalPayments({
    clientOrderId: 'x',
    totalAmount: 10000,
    paymentMethod: 'banking',
  })
  assert(pays.length === 1, '3 payments undefined → one fallback line')
  assertEq(pays[0].amount, 10000, '3 synthetic amount = totalAmount')
}

// 4. payments empty → no crash
{
  const pays = mapLocalPayments({
    clientOrderId: 'y',
    totalAmount: 12000,
    payments: [],
    paymentMethod: 'cash',
  })
  assert(pays.length === 1, '4 empty payments → synthetic line')
  assertEq(pays[0].amount, 12000, '4 empty payments amount')
}

// 5. payment item missing amount + single source → may use order total for line
{
  const pays = mapLocalPayments({
    clientOrderId: 'z',
    totalAmount: 99000,
    paymentSnapshot: {
      method: 'cash',
      paymentMethodId: 1,
      receivedAmount: 100000,
      changeAmount: 1000,
      capturedAt: '2026-07-10T00:00:00.000Z',
    } as never,
  })
  assertEq(pays[0].amount, 99000, '5 single pay missing amount → order total for line')
  assert(pays[0].amount !== 100000, '5 does not use receivedAmount as line amount')
  assert(pays[0].amount !== 1000, '5 does not use changeAmount as line amount')
}

// 6. Old cash order missing receivedAmount/changeAmount → still renders
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'old-cash',
    totalAmount: 35000,
    paymentMethod: 'cash',
    soldAt: '2026-06-01T00:00:00.000Z',
    paymentSnapshot: {
      method: 'cash',
      paymentMethodId: 1,
      amount: 35000,
      capturedAt: '2026-06-01T00:00:00.000Z',
    } as never,
    cartSnapshot: [{ menuItemId: 1, name: 'Latte', quantity: 1, unitPrice: 35000 }],
    items: [],
    syncStatus: 'Failed',
  })
  assertEq(row.payments[0]?.amount, 35000, '6 old cash amount ok')
  assertEq(row.payments[0]?.receivedAmount, null, '6 missing received → null not 0')
  assertEq(row.payments[0]?.changeAmount, null, '6 missing change → null not 0')
  assertEq(formatHistoryMoney(row.payments[0]?.receivedAmount), ORDER_HISTORY_LABELS.noMoneyData, '6 format null money')
}

// 7. cartSnapshot partial → no crash
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'partial-cart',
    totalAmount: 20000,
    paymentSnapshot: {
      method: 'cash', paymentMethodId: 1, amount: 20000, receivedAmount: 20000, changeAmount: 0, capturedAt: 'x',
    },
    cartSnapshot: [{ menuItemId: 9, name: '', quantity: undefined as never, unitPrice: undefined as never }],
    items: [],
    syncStatus: 'Pending',
  })
  assert(row.items.length === 1, '7 partial cart → still one line')
  assert(row.items[0].drinkName.startsWith('Món'), '7 missing name → placeholder')
  assertEq(row.items[0].quantity, 0, '7 missing qty → 0 (line meta only)')
}

// 8. Mixed good + bad
{
  const rows = mapLocalOrdersSafe([
    {
      clientOrderId: 'good',
      totalAmount: 10000,
      paymentSnapshot: {
        method: 'cash', paymentMethodId: 1, amount: 10000, receivedAmount: 10000, changeAmount: 0, capturedAt: 'x',
      },
      cartSnapshot: [{ menuItemId: 1, name: 'A', quantity: 1, unitPrice: 10000 }],
      items: [],
      syncStatus: 'Pending',
      soldAt: '2026-07-10T12:00:00.000Z',
    },
    null,
    { clientOrderId: 'corrupt-ish' },
  ])
  assertEq(rows.length, 3, '8 mixed → all three rows returned')
  assert(rows[0].total === 10000 && rows[0].isDegraded !== true, '8 first good')
  assert(rows[1].isDegraded === true, '8 null → degraded card')
  assert(rows[2].isDegraded === true, '8 incomplete → degraded card')
}

// 9. One corrupt record does not throw; others still map
{
  let threw = false
  let rows
  try {
    rows = mapLocalOrdersSafe([
      { clientOrderId: 'ok', totalAmount: 5000, cartSnapshot: [], items: [{ menuItemId: 1, name: 'B', quantity: 1, unitPrice: 5000 }], paymentSnapshot: { method: 'cash', paymentMethodId: 1, amount: 5000, receivedAmount: 5000, changeAmount: 0, capturedAt: 'x' }, syncStatus: 'Pending' },
      undefined,
      { clientOrderId: 'bad', paymentSnapshot: null, cartSnapshot: null, items: null },
    ])
  } catch {
    threw = true
  }
  assert(!threw, '9 mapLocalOrdersSafe never throws')
  assert(rows!.length === 3, '9 three results')
  assert(rows![0].clientOrderId === 'ok', '9 first still present')
}

// 10. Empty input → empty list
{
  assertEq(mapLocalOrdersSafe([]).length, 0, '10 empty array')
  assertEq(mapLocalOrdersSafe(null).length, 0, '10 null array')
  assertEq(mapLocalOrdersSafe(undefined).length, 0, '10 undefined array')
}

// 11. No trustworthy money → null + label
{
  assertEq(pickMoney(undefined, null, 'x'), null, '11 pickMoney invalid → null')
  assertEq(formatHistoryMoney(null), ORDER_HISTORY_LABELS.noMoneyData, '11 format null')
  const row = mapLocalOrderSafe({ clientOrderId: 'no-money', cartSnapshot: [], items: [], syncStatus: 'Pending' })
  assertEq(row.total, null, '11 no money fields → total null')
  assertEq(formatHistoryMoney(row.total), ORDER_HISTORY_LABELS.noMoneyData, '11 display label')
}

// 12. Old IndexedDB-like fixture (legacy payment + cart aliases)
{
  const row = mapLocalOrderSafe({
    queueId: 99,
    clientOrderId: 'legacy-schema-v1',
    soldAt: '2026-05-01T00:00:00.000Z',
    total: 55000,
    paymentMethod: 'cash',
    payment: { method: 'cash', amount: 55000, capturedAt: '2026-05-01T00:00:00.000Z' },
    cart: [{ menuItemId: 3, name: 'Matcha', quantity: 1, unitPrice: 55000 }],
    syncStatus: 'Failed',
    retryCount: 2,
    lastError: 'network',
  })
  assertEq(row.total, 55000, '12 legacy total field')
  assertEq(row.payments[0]?.amount, 55000, '12 legacy payment object')
  assert(row.items[0]?.drinkName === 'Matcha', '12 legacy cart alias')
  assert(row.syncState === ORDER_HISTORY_LABELS.syncFailed, '12 sync failed label')
}

// 13. Split payment: totalAmount 100_000, lines 40k + 60k — total stays 100k
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'split-100k',
    totalAmount: 100000,
    payments: [
      { method: 'cash', amount: 40000, capturedAt: '2026-07-11T00:00:00.000Z' },
      { method: 'banking', amount: 60000, capturedAt: '2026-07-11T00:00:00.000Z' },
    ],
    cartSnapshot: [{ menuItemId: 1, name: 'Combo', quantity: 1, unitPrice: 100000 }],
    items: [],
    syncStatus: 'Pending',
    soldAt: '2026-07-11T00:00:00.000Z',
  })
  assertEq(row.total, 100000, '13 split → order total = totalAmount 100000')
  assert(row.total !== 40000, '13 first payment must not override order total')
  assertEq(row.payments[0]?.amount, 40000, '13 first line amount 40000')
  assertEq(row.payments[1]?.amount, 60000, '13 second line amount 60000')
  assertEq(resolveOrderTotal({
    totalAmount: 100000,
    payments: [{ amount: 40000 }, { amount: 60000 }],
  }), 100000, '13 resolveOrderTotal prefers totalAmount')
}

// 14. Missing order total + multi payment → do not guess (null)
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'split-no-total',
    payments: [
      { method: 'cash', amount: 40000 },
      { method: 'banking', amount: 60000 },
    ],
    cartSnapshot: [{ menuItemId: 1, name: 'X', quantity: 1, unitPrice: 100000 }],
    items: [],
    syncStatus: 'Pending',
  })
  assertEq(row.total, null, '14 multi payment no total → null (no first-line / no sum)')
  assertEq(formatHistoryMoney(row.total), ORDER_HISTORY_LABELS.noMoneyData, '14 display Chưa có dữ liệu')
  assertEq(row.payments[0]?.amount, 40000, '14 lines still map individually')
  assertEq(row.payments[1]?.amount, 60000, '14 second line ok')
}

// 15. Single payment legacy fallback only when semantics certain
{
  assertEq(
    resolveOrderTotal({
      payments: [{ amount: 77000 }],
    }),
    77000,
    '15 single payment no total → use that payment amount',
  )
  assertEq(
    resolveOrderTotal({
      payments: [{ amount: 40000 }, { amount: 60000 }],
    }),
    null,
    '15 multi payment no total → null',
  )
  assertEq(
    resolveOrderTotal({
      totalAmount: 100000,
      payments: [{ amount: 40000 }],
    }),
    100000,
    '15 totalAmount always wins over payment',
  )
  // received/change never total
  assertEq(
    resolveOrderTotal({
      paymentSnapshot: {
        method: 'cash',
        paymentMethodId: 1,
        receivedAmount: 200000,
        changeAmount: 50000,
        capturedAt: 'x',
      } as never,
    }),
    null,
    '15 received/change alone → null order total',
  )
}

// 16. Corrupt safety: non-array cart, null payment items, invalid date, unknown input
{
  const row = mapLocalOrderSafe({
    clientOrderId: 'corrupt-shapes',
    totalAmount: 15000,
    cartSnapshot: 'not-an-array' as never,
    items: 'also-bad' as never,
    payments: [null, undefined, { method: 'cash', amount: 15000 }],
    soldAt: 'not-a-date',
    createdAt: 'bad',
    syncStatus: 'Pending',
  })
  assert(row.items.length === 0, '16 non-array cart → empty items not crash')
  assert(row.payments.length === 1, '16 null payment items filtered')
  assertEq(row.total, 15000, '16 total still from totalAmount')
  assert(typeof row.soldAt === 'string', '16 soldAt always string')
  assert(!Number.isNaN(new Date(row.soldAt).getTime()), '16 soldAt parseable')

  const unknownRow = mapLocalOrderSafe('garbage')
  assert(unknownRow.isDegraded === true, '16 unknown input → degraded')
  assertEq(unknownRow.total, null, '16 unknown total null')

  const noId = mapLocalOrderSafe({ totalAmount: 1, cartSnapshot: [], items: [] })
  assert(noId.clientOrderId === 'local-offline-order' || noId.clientOrderId.startsWith('queue-'), '16 missing clientOrderId stable key')
  assert(noId.key.length > 0, '16 render key stable')
}

// 17. Multi-payment missing one line amount → that line null, not order total
{
  const pays = mapLocalPayments({
    totalAmount: 100000,
    payments: [
      { method: 'cash', amount: 40000 },
      { method: 'banking' }, // missing amount
    ],
  })
  assertEq(pays[0].amount, 40000, '17 first line ok')
  assertEq(pays[1].amount, null, '17 second line null (do not borrow order total)')
}

console.log(`\nResult: ${passed} passed, ${failed} failed`)
if (failed > 0) {
  throw new Error(`Self-test failed: ${failed} assertion(s)`)
}
console.log('All mapper self-tests passed.')
