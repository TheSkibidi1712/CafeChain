const assert = require('node:assert/strict');
const path = require('node:path');
const test = require('node:test');

const interactionsPath = path.resolve(
    __dirname,
    '../../CafeChain/wwwroot/js/Admin/AIImport/ai-import-interactions.js');
const {
    createAlertCoordinator,
    createOperationGuard
} = require(interactionsPath);

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((resolvePromise, rejectPromise) => {
        resolve = resolvePromise;
        reject = rejectPromise;
    });
    return { promise, reject, resolve };
}

test('double activation of an AI Import mutation runs the operation once', async () => {
    const transitions = [];
    const operation = deferred();
    let executionCount = 0;
    const guard = createOperationGuard((busy, key) => transitions.push([busy, key]));

    const first = guard.run('save-item:42', async () => {
        executionCount++;
        return operation.promise;
    });
    const duplicate = await guard.run('save-item:42', async () => {
        executionCount++;
    });

    assert.deepEqual(duplicate, { started: false });
    assert.equal(executionCount, 1);
    assert.equal(guard.isBusy(), true);

    operation.resolve('saved');
    assert.deepEqual(await first, { started: true, value: 'saved' });
    assert.equal(guard.isBusy(), false);
    assert.deepEqual(transitions, [
        [true, 'save-item:42'],
        [false, 'save-item:42']
    ]);
});

test('a failed mutation releases the AI Import operation guard', async () => {
    const guard = createOperationGuard();

    await assert.rejects(
        guard.run('skip-item:42', async () => {
            throw new Error('stale preview');
        }),
        /stale preview/);

    assert.equal(guard.isBusy(), false);
    assert.deepEqual(
        await guard.run('skip-item:42', async () => 'retried'),
        { started: true, value: 'retried' });
});

test('duplicate AI Import feedback shares one popup and uses the top layer', async () => {
    const popup = deferred();
    const target = {};
    const calls = [];
    const swal = {
        fire(options) {
            calls.push(options);
            return popup.promise;
        },
        close() {}
    };
    const alerts = createAlertCoordinator({ swal, target });

    const first = alerts.show('save-item:42:success', { title: 'Đã lưu' });
    const duplicate = alerts.show('save-item:42:success', { title: 'Đã lưu' });
    await new Promise(resolve => setImmediate(resolve));

    assert.equal(calls.length, 1);
    assert.equal(calls[0].target, target);
    assert.equal(calls[0].topLayer, true);

    popup.resolve({ isConfirmed: true });
    assert.deepEqual(await first, { isConfirmed: true });
    assert.deepEqual(await duplicate, { isConfirmed: true });
});

test('closing AI Import feedback cancels queued popups and delegates cleanup', async () => {
    const popup = deferred();
    let closeCount = 0;
    let fireCount = 0;
    const swal = {
        fire() {
            fireCount++;
            return popup.promise;
        },
        close() {
            closeCount++;
            popup.resolve({ isDismissed: true });
        }
    };
    const alerts = createAlertCoordinator({ swal, target: {} });

    const active = alerts.show('first', { title: 'First' });
    const queued = alerts.show('second', { title: 'Second' });
    await new Promise(resolve => setImmediate(resolve));
    alerts.close();

    assert.deepEqual(await active, { isDismissed: true });
    assert.deepEqual(await queued, { isDismissed: true, cancelled: true });
    assert.equal(fireCount, 1);
    assert.equal(closeCount, 1);
});
