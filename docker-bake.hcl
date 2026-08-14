variable "VERSION" { default = "0.0.0" }
variable "REVISION" { default = "unknown" }

group "default" { targets = ["api", "web", "migrator"] }

target "common" {
  platforms = ["linux/amd64"]
  args = { VERSION = "${VERSION}", REVISION = "${REVISION}" }
  labels = {
    "org.opencontainers.image.source" = "https://github.com/euzebioalves/AxiomAtlas"
    "org.opencontainers.image.version" = "${VERSION}"
    "org.opencontainers.image.revision" = "${REVISION}"
  }
  attest = ["type=provenance,mode=max", "type=sbom"]
}

target "api" {
  inherits = ["common"]
  dockerfile = "Axiom.Atlas.API/Dockerfile"
  tags = ["ghcr.io/euzebioalves/axiomatlas-api:${VERSION}", "ghcr.io/euzebioalves/axiomatlas-api:sha-${REVISION}"]
}

target "web" {
  inherits = ["common"]
  dockerfile = "Axiom.Atlas.Web/Dockerfile"
  tags = ["ghcr.io/euzebioalves/axiomatlas-web:${VERSION}", "ghcr.io/euzebioalves/axiomatlas-web:sha-${REVISION}"]
}

target "migrator" {
  inherits = ["common"]
  dockerfile = "Axiom.Atlas.Migrator/Dockerfile"
  tags = ["ghcr.io/euzebioalves/axiomatlas-migrator:${VERSION}", "ghcr.io/euzebioalves/axiomatlas-migrator:sha-${REVISION}"]
}
