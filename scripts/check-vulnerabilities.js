#!/usr/bin/env node

const fs = require("fs");
const input = process.argv[2];
if (!input) {
    console.error("Usage: check-vulnerabilities.js <dotnet-vulnerabilities.json>");
    process.exit(1);
}
const report = JSON.parse(fs.readFileSync(input, "utf8"));
const findings = [];
function visit(value, packageName) {
    if (Array.isArray(value)) {
        for (const item of value) visit(item, packageName);
        return;
    }
    if (!value || typeof value !== "object") return;
    const name = value.name || value.id || packageName;
    if (Array.isArray(value.vulnerabilities)) {
        for (const vulnerability of value.vulnerabilities) {
            findings.push({
                packageName: name || "unknown package",
                severity: String(vulnerability.severity || "Unknown"),
                advisory: vulnerability.advisoryurl || vulnerability.advisoryUrl || vulnerability.url || "no advisory URL"
            });
        }
    }
    for (const [key, child] of Object.entries(value)) {
        if (key !== "vulnerabilities") visit(child, name);
    }
}
visit(report);
const normalized = findings.map((finding) => ({ ...finding, severity: finding.severity.toLowerCase() }));
if (normalized.length === 0) {
    console.log("No vulnerable direct or transitive package was reported.");
    process.exit(0);
}
for (const finding of normalized) {
    console.log(`${finding.severity.toUpperCase()} | ${finding.packageName} | ${finding.advisory}`);
}
if (normalized.some((finding) => ["critical", "high"].includes(finding.severity))) {
    console.error("Critical or High dependency vulnerability detected.");
    process.exit(1);
}
