const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const extension = path.join(__dirname, "..", "browser-extension", "yandex");
const rules = JSON.parse(fs.readFileSync(path.join(extension, "rules.json"), "utf8"));

test("rules match short feeds without matching ordinary pages", () => {
    const patterns = rules.map(rule => {
        assert.equal(rule.action.type, "block");
        assert.deepEqual(rule.condition.resourceTypes, ["main_frame"]);
        return new RegExp(rule.condition.regexFilter);
    });
    const cases = [
        ["https://m.youtube.com/shorts/demo", true],
        ["https://vk.com/clip-123_456?from=feed", true],
        ["https://m.vkvideo.ru/clips/club123", true],
        ["https://rutube.ru/feeds/shorts/", true],
        ["https://rutube.ru/u/rutube/shorts/", true],
        ["https://www.tiktok.com/foryou", true],
        ["https://www.tiktok.com/@example/video/123456", true],
        ["https://m.youtube.com/watch?v=demo", false],
        ["https://vk.com/feed?z=video-123_456", false],
        ["https://m.vkvideo.ru/video-123_456", false],
        ["https://rutube.ru/info/shorts", false],
        ["https://rutube.ru/video/demo", false],
        ["https://www.tiktok.com/@example/photo/123456", false],
        ["https://www.tiktok.com/", false],
        ["https://example.org/?url=https://vk.com/clips", false],
        ["https://vk.com.example.org/clips", false],
        ["https://vk.com@invalid.example/clips", false]
    ];
    for (const [address, shouldBlock] of cases) {
        assert.equal(patterns.some(pattern => pattern.test(address)), shouldBlock, address);
    }
});

test("worker intercepts a current top-level route change and ignores stale events", async () => {
    const listeners = [];
    const updates = [];
    const shortUrl = "https://rutube.ru/feeds/shorts/";
    const normalUrl = "https://rutube.ru/";
    let currentUrl = shortUrl;
    const addListener = (listener, filter) => {
        assert(filter.url.some(item => item.hostEquals === "rutube.ru"));
        listeners.push(listener);
    };
    const chrome = {
        runtime: { getURL: file => "chrome-extension://bss/" + file },
        webNavigation: {
            onCommitted: { addListener },
            onHistoryStateUpdated: { addListener },
            getFrame: async () => ({ url: currentUrl })
        },
        tabs: { update: async (...args) => updates.push(args) }
    };
    vm.runInNewContext(fs.readFileSync(path.join(extension, "service-worker.js"), "utf8"), {
        chrome,
        fetch: async url => {
            assert.equal(url, "chrome-extension://bss/rules.json");
            return { json: async () => rules };
        }
    });
    assert.equal(listeners.length, 2);
    const onHistoryStateUpdated = listeners[0];
    const navigation = { frameId: 0, tabId: 3 };
    await onHistoryStateUpdated({ ...navigation, frameId: 1, url: shortUrl });
    await onHistoryStateUpdated({ ...navigation, url: normalUrl });
    await onHistoryStateUpdated({ ...navigation, url: shortUrl });
    currentUrl = normalUrl;
    await onHistoryStateUpdated({ ...navigation, url: shortUrl });
    assert.equal(updates.length, 1);
    assert.equal(updates[0][0], 3);
    assert.equal(updates[0][1].url, "chrome-extension://bss/blocked.html");
});
