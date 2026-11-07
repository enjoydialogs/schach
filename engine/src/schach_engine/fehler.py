"""Fehler mit Status und Code nach engine/API.md."""


class EngineFehler(Exception):
    """Wird zu `{error, detail}` mit Status nach engine/API.md. `detail` nie mit Spielerdaten."""

    def __init__(self, status: int, code: str, detail: str) -> None:
        super().__init__(detail)
        self.status = status
        self.code = code
        self.detail = detail
