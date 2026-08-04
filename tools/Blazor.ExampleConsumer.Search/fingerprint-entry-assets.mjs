import { createHash } from "node:crypto";
import { copyFile, readFile, stat, writeFile } from "node:fs/promises";
import { dirname, extname, basename, isAbsolute, relative, resolve, sep } from "node:path";

const publishRoot = resolve(requiredArgument(2, "publish root"));
const indexPath = resolve(
    process.argv[3] ?? resolve(publishRoot, "index.html"));
const sourceHtml = await readFile(indexPath, "utf8");
const assetTagPattern = /(<(?:link|script)\b[^>]*?\b(?:href|src)=")([^"]+)(")/gi;
const fingerprintedAssets = new Map();

let outputHtml = "";
let previousIndex = 0;

for (const match of sourceHtml.matchAll(assetTagPattern)) {
    outputHtml += sourceHtml.slice(previousIndex, match.index);
    const assetUrl = await fingerprintEntryAsset(match[2]);
    outputHtml += `${match[1]}${assetUrl}${match[3]}`;
    previousIndex = match.index + match[0].length;
}

outputHtml += sourceHtml.slice(previousIndex);
await writeFile(indexPath, outputHtml);

for (const [source, destination] of fingerprintedAssets) {
    console.log(`Fingerprinting ${source} -> ${destination}`);
}

console.log(`Fingerprinted ${fingerprintedAssets.size} HTML entry assets.`);

async function fingerprintEntryAsset(assetUrl) {
    if (isExternalUrl(assetUrl)) {
        return assetUrl;
    }

    const suffixIndex = assetUrl.search(/[?#]/);
    const assetPath = suffixIndex >= 0
        ? assetUrl.slice(0, suffixIndex)
        : assetUrl;
    const suffix = suffixIndex >= 0
        ? assetUrl.slice(suffixIndex)
        : "";
    const extension = extname(assetPath).toLowerCase();

    if (extension !== ".css" && extension !== ".js") {
        return assetUrl;
    }

    if (/\.[a-z0-9]{8,}\.(?:css|js)$/i.test(assetPath)) {
        return assetUrl;
    }

    const sourcePath = resolve(publishRoot, assetPath);
    assertInsidePublishRoot(sourcePath);
    const sourceStats = await stat(sourcePath);
    if (!sourceStats.isFile()) {
        throw new Error(`Entry asset '${assetPath}' is not a file.`);
    }

    const contents = await readFile(sourcePath);
    const fingerprint = createHash("sha256")
        .update(contents)
        .digest("hex")
        .slice(0, 12);
    const outputName = `${basename(assetPath, extension)}.${fingerprint}${extension}`;
    const outputPath = resolve(dirname(sourcePath), outputName);
    const outputAssetPath = relative(publishRoot, outputPath).split(sep).join("/");

    await copyFile(sourcePath, outputPath);
    fingerprintedAssets.set(assetPath, outputAssetPath);
    return `${outputAssetPath}${suffix}`;
}

function requiredArgument(index, name) {
    const value = process.argv[index];
    if (!value) {
        throw new Error(`Missing ${name} argument.`);
    }
    return value;
}

function isExternalUrl(value) {
    return isAbsolute(value)
        || /^(?:[a-z][a-z0-9+.-]*:|\/\/|#)/i.test(value);
}

function assertInsidePublishRoot(filePath) {
    const relativePath = relative(publishRoot, filePath);
    if (relativePath.startsWith("..") || isAbsolute(relativePath)) {
        throw new Error(`Entry asset '${filePath}' is outside the publish root.`);
    }
}
