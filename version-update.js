#!/usr/bin/env node

const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const defaultProjectPath = path.resolve(
    __dirname,
    "Axiom.Atlas.Web",
    "Axiom.Atlas.Web.csproj"
);

let projectPath = defaultProjectPath;
let minimumVersion;
for (let index = 2; index < process.argv.length; index += 1) {
    const argument = process.argv[index];

    if (argument === "--file" || argument === "--minimum") {
        const value = process.argv[index + 1];

        if (!value) {
            console.error(`Missing value for ${argument}.`);
            process.exit(1);
        }

        if (argument === "--file") {
            projectPath = path.resolve(process.cwd(), value);
        } else {
            minimumVersion = value;
        }

        index += 1;
        continue;
    }

    console.error(`Unknown argument: ${argument}.`);
    process.exit(1);
}

function parseVersion(value, source) {
    const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(value.trim());

    if (!match) {
        throw new Error(`${source} must use the MAJOR.MINOR.PATCH format.`);
    }

    return {
        major: Number.parseInt(match[1], 10),
        minor: Number.parseInt(match[2], 10),
        patch: Number.parseInt(match[3], 10)
    };
}

function compareVersions(left, right) {
    for (const key of ["major", "minor", "patch"]) {
        if (left[key] !== right[key]) {
            return left[key] - right[key];
        }
    }

    return 0;
}

function incrementPatch(version) {
    return `${version.major}.${version.minor}.${version.patch + 1}`;
}

function stableTags() {
    return execFileSync("git", ["tag", "--list", "v*"], { encoding: "utf8" })
        .split(/\r?\n/)
        .filter(Boolean)
        .filter((tag) => /^v\d+\.\d+\.\d+$/.test(tag))
        .map((tag) => parseVersion(tag.slice(1), `Git tag ${tag}`));
}

try {
    const projectFile = fs.readFileSync(projectPath, "utf8");
    const versionMatch = projectFile.match(
        /<VersionPrefix>\s*([^<]+?)\s*<\/VersionPrefix>/
    );

    if (!versionMatch) {
        throw new Error("VersionPrefix was not found in the project file.");
    }

    const candidates = [parseVersion(versionMatch[1], "VersionPrefix"), ...stableTags()];
    if (minimumVersion) {
        candidates.push(parseVersion(minimumVersion, "Minimum version"));
    }
    const baseline = candidates.reduce((latest, candidate) =>
        compareVersions(candidate, latest) > 0 ? candidate : latest
    );
    const nextVersion = incrementPatch(baseline);

    process.stdout.write(nextVersion);
} catch (error) {
    console.error(`Could not calculate the next Axiom Atlas version: ${error.message}`);
    process.exit(1);
}
