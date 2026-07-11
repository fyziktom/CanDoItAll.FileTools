const objectUrls = new WeakMap();

export function applyObjectUrl(element, bytes, mediaType, attributeName) {
    revokeObjectUrl(element);
    const url = URL.createObjectURL(new Blob([bytes], { type: mediaType }));
    objectUrls.set(element, { url, attributeName });
    element.setAttribute(attributeName, url);
}

export function revokeObjectUrl(element) {
    const entry = objectUrls.get(element);
    if (entry) {
        if (element.getAttribute(entry.attributeName) === entry.url) {
            element.removeAttribute(entry.attributeName);
        }

        URL.revokeObjectURL(entry.url);
        objectUrls.delete(element);
    }
}
