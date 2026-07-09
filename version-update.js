const fs = require('fs');
const path = require('path');

// Caminho para o seu package.json
const packageJsonPath = path.resolve(__dirname, 'package.json');

// Lendo o arquivo atual
const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));

// Lógica de Incremento (Major.Minor.Patch)
const versionParts = packageJson.version.split('.');
let major = parseInt(versionParts[0]);
let minor = parseInt(versionParts[1]);
let patch = parseInt(versionParts[2]);

// Incrementa o Patch automaticamente a cada build
patch++;

// Regra opcional: Se o patch chegar a 100, sobe o minor (exemplo de regra de negócio)
if (patch > 99) {
    patch = 0;
    minor++;
}

const newVersion = `${major}.${minor}.${patch}`;
packageJson.version = newVersion;

// Salva o arquivo atualizado
fs.writeFileSync(packageJsonPath, JSON.stringify(packageJson, null, 2));

console.log(`✅ Versão do Axiom Atlas atualizada para: ${newVersion}`);