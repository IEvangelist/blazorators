import { readFile, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import * as pagefind from "pagefind";

const toolDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(toolDirectory, "..", "..");
const catalogPath = resolve(
    repositoryRoot,
    "samples",
    "Blazor.ExampleConsumer",
    "wwwroot",
    "search-catalog.json");
const outputPath = resolve(
    repositoryRoot,
    process.argv[2] ?? "artifacts/pagefind");
const shellPath = process.argv[3]
    ? resolve(repositoryRoot, process.argv[3])
    : null;

const records = JSON.parse(await readFile(catalogPath, "utf8"));
validateRecords(records);

await rm(outputPath, { recursive: true, force: true });

const { index } = await pagefind.createIndex({
    forceLanguage: "en",
    includeCharacters: "._#",
    writePlayground: false,
    verbose: false
});

try {
    for (const record of records) {
        const { errors } = await index.addCustomRecord({
            url: record.path ? `/${record.path}/` : "/",
            content: [
                record.title,
                record.summary,
                record.content,
                ...record.keywords
            ].join("\n"),
            language: "en",
            meta: {
                title: record.title,
                category: record.category,
                summary: record.summary,
                icon: record.icon
            },
            filters: {
                category: [record.category]
            },
            sort: {
                weight: String(record.weight)
            }
        });

        if (errors.length > 0) {
            throw new Error(
                `Pagefind failed to add '${record.path || "/"}': ${errors.join("; ")}`);
        }
    }

    const { errors } = await index.writeFiles({ outputPath });
    if (errors.length > 0) {
        throw new Error(`Pagefind failed to write the index: ${errors.join("; ")}`);
    }

    if (shellPath) {
        await markShellAsIndexed(shellPath);
    }

    console.log(`Pagefind indexed ${records.length} Blazorators routes.`);
} finally {
    await index.deleteIndex();
    await pagefind.close();
}

function validateRecords(items) {
    if (!Array.isArray(items) || items.length === 0) {
        throw new Error("The search catalog must contain at least one route.");
    }

    const paths = new Set();
    for (const item of items) {
        for (const property of ["path", "title", "category", "summary", "content", "icon"]) {
            if (typeof item[property] !== "string") {
                throw new Error(`Search record '${item.title ?? "unknown"}' is missing '${property}'.`);
            }
        }

        if (!Array.isArray(item.keywords) || item.keywords.some(keyword => typeof keyword !== "string")) {
            throw new Error(`Search record '${item.title}' has invalid keywords.`);
        }

        if (!Number.isFinite(item.weight)) {
            throw new Error(`Search record '${item.title}' has an invalid weight.`);
        }

        if (paths.has(item.path)) {
            throw new Error(`Search route '${item.path}' is duplicated.`);
        }
        paths.add(item.path);
    }
}

async function markShellAsIndexed(path) {
    const marker = '    <meta name="blazorators-search-provider" content="pagefind" />\n';
    let html = await readFile(path, "utf8");
    if (html.includes('name="blazorators-search-provider"')) {
        html = html.replace(
            /    <meta name="blazorators-search-provider"[^>]*>\r?\n/,
            marker);
    } else {
        html = html.replace("</head>", `${marker}</head>`);
    }
    await writeFile(path, html, "utf8");
}
