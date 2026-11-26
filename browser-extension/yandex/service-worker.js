const domains = ["youtube.com", "vk.com", "vkvideo.ru", "rutube.ru", "tiktok.com"];
const navigationFilter = {
    url: domains.flatMap(domain => ["", "www.", "m."].map(prefix => ({
        hostEquals: prefix + domain,
        schemes: ["https", "http"]
    })))
};
let patterns;

async function blockShorts(navigation) {
    if (navigation.frameId !== 0 || navigation.tabId < 0 ||
        (navigation.documentLifecycle && navigation.documentLifecycle !== "active")) return;

    try {
        // Правила переходов внутри страницы совпадают с правилами сетевой блокировки.
        patterns ??= fetch(chrome.runtime.getURL("rules.json"))
            .then(response => response.json())
            .then(rules => rules.map(rule => new RegExp(rule.condition.regexFilter)));
        const rules = await patterns;
        if (!rules.some(rule => rule.test(navigation.url))) return;

        const frame = await chrome.webNavigation.getFrame({ tabId: navigation.tabId, frameId: 0 });
        if (frame?.url === navigation.url) {
            await chrome.tabs.update(navigation.tabId, { url: chrome.runtime.getURL("blocked.html") });
        }
    } catch {
        // Вкладку могли закрыть, пока проверялся переход.
    }
}

chrome.webNavigation.onHistoryStateUpdated.addListener(blockShorts, navigationFilter);
chrome.webNavigation.onCommitted.addListener(blockShorts, navigationFilter);
