#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const defaultProjectPath = path.resolve(
    __dirname,
    "Axiom.Atlas.Web",
    "Axiom.Atlas.Web.csproj"
);

let projectPath = defaultProjectPath;
let minimumVersion;
let dryRun = false;

for (let index = 0; index < process.argv.length - 2; index += 1) {
    const argument = process.argv[index + 2];

    if (argument === "--dry-run") {
        dryRun = true;
        continue;
    }

    if (argument === "--file" || argument === "--minimum") {
        const value = process.argv[index + 3];

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
    let { major, minor, patch } = version;

    patch += 1;

    if (patch > 99) {
        patch = 0;
        minor += 1;
    }

    return `${major}.${minor}.${patch}`;
}

try {
    const projectFile = fs.readFileSync(projectPath, "utf8");
    const versionMatch = projectFile.match(
        /<VersionPrefix>\s*([^<]+?)\s*<\/VersionPrefix>/
    );

    if (!versionMatch) {
        throw new Error("VersionPrefix was not found in the project file.");
    }

    const projectVersion = parseVersion(versionMatch[1], "VersionPrefix");
    const minimum = minimumVersion
        ? parseVersion(minimumVersion, "Minimum version")
        : undefined;
    const baseline =
        minimum && compareVersions(projectVersion, minimum) < 0
            ? minimum
            : projectVersion;
    const nextVersion = incrementPatch(baseline);

    if (!dryRun) {
        const updatedProjectFile = projectFile.replace(
            versionMatch[0],
            `<VersionPrefix>${nextVersion}</VersionPrefix>`
        );

        fs.writeFileSync(projectPath, updatedProjectFile);
    }

    process.stdout.write(nextVersion);
} catch (error) {
    console.error(`Could not update the Axiom Atlas version: ${error.message}`);
    process.exit(1);
}
