"""Parse a scanned CEPEM paper intake form.

The paper form is a fixed two-page A4 layout. We combine three signals:

* the PDF text layer when the scan is searchable,
* Tesseract OCR over the rendered page, and
* simple computer vision over the printed table grid (ticked boxes).

Handwriting OCR is inherently unreliable; every field is returned with a
review warning and the digitizer always confirms the values in the SPA.
"""

from __future__ import annotations

import base64
import io
import os
import re
import unicodedata
from dataclasses import dataclass, field
from typing import Any

import cv2
import pymupdf as fitz  # PyMuPDF (the legacy "fitz" alias is deprecated)
import numpy as np
import pytesseract
from PIL import Image

RENDER_DPI = 200
OCR_LANG = os.getenv("TESSERACT_LANG", "ces+eng")

TITLE_KEYWORDS = (
    "vstupni formular",
    "vstupní formulář",
    "vstupni formular klienta",
    "vstupní formulář klienta",
    "formular klienta",
    "formulář klienta",
    "prijmeni",
    "příjmení",
    "datum narozeni",
    "datum narození",
    "zdravotni pojistovna",
    "zdravotní pojišťovna",
    "smluvni podminky",
    "smluvní podmínky",
)

# Strong title markers: their presence alone identifies the CEPEM paper form
# even when OCR mangles the rest of the header.
TITLE_MARKERS = (
    "vstupni formular",
    "vstupní formulář",
    "formular klienta",
    "formulář klienta",
)

# Field boxes as fractions of the page (x0, y0, x1, y1). Calibrated on the
# current printed form; the parser tolerates a few percent of drift.
PAGE1_FIELDS = {
    "lastName": (0.06, 0.075, 0.24, 0.125),
    "firstName": (0.38, 0.075, 0.53, 0.125),
    "dateOfBirth": (0.73, 0.075, 0.86, 0.125),
    "weight": (0.075, 0.135, 0.14, 0.175),
    "height": (0.40, 0.135, 0.47, 0.175),
    "insuranceCompany": (0.73, 0.135, 0.82, 0.175),
    "email": (0.06, 0.195, 0.36, 0.235),
    "phone": (0.45, 0.195, 0.61, 0.235),
    "postalCode": (0.80, 0.195, 0.88, 0.23),
    "address": (0.72, 0.215, 0.98, 0.25),
}

MED_GRID_KEYS = (
    ("medBloodPressure", "medHeart", "medCholesterol"),
    ("medBloodThinners", "medDiabetes", "medThyroid"),
    ("medNerves", "medPsych", "medDigestion"),
    ("medPain", "medDehydration", "medBreathing"),
    ("medAntibiotics", "medSupplements", "medAllergies"),
)

LIFESTYLE_GRID_KEYS = (
    ("poorSleep", "digestiveIssues", "physicalStress"),
    ("mentalStress", "smoking", "fatigue"),
)

# Tick-box columns of the printed tables (fractions of the page width).
MED_COLUMNS = ((0.275, 0.40), (0.62, 0.755), (0.895, 0.985))
MED_ROWS = ((0.330, 0.395), (0.395, 0.455), (0.455, 0.520), (0.520, 0.580), (0.580, 0.640))
LIFESTYLE_COLUMNS = ((0.225, 0.345), (0.575, 0.685), (0.895, 0.985))
LIFESTYLE_ROWS = ((0.540, 0.605), (0.605, 0.665))

DATE_RE = re.compile(r"(\d{1,2})\s*[.\-/ ]\s*(\d{1,2})\s*[.\-/ ]\s*(\d{4})")
EMAIL_RE = re.compile(r"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")
PHONE_RE = re.compile(r"(?:\+?\d[\d\s\-]{6,}\d)")
INSURANCE_RE = re.compile(r"\b(\d{3})\b")


@dataclass
class PageImage:
    rgb: np.ndarray
    gray: np.ndarray
    png_bytes: bytes


@dataclass
class ExtractionResult:
    is_intake_form: bool
    confidence: float
    language: str = "cs"
    fields: dict[str, Any] = field(default_factory=dict)
    warnings: list[str] = field(default_factory=list)
    text: str = ""
    page_count: int = 0
    page_texts: list[str] = field(default_factory=list)
    preview_pages: list[dict[str, Any]] = field(default_factory=list)
    regions: list[dict[str, Any]] = field(default_factory=list)

    def as_dict(self) -> dict[str, Any]:
        return {
            "isIntakeForm": self.is_intake_form,
            "confidence": round(self.confidence, 3),
            "language": self.language,
            "fields": self.fields,
            "warnings": self.warnings,
            "text": self.text,
            "pageCount": self.page_count,
            "pageTexts": self.page_texts,
            "previewPages": self.preview_pages,
            "regions": self.regions,
        }


def _normalize(text: str) -> str:
    text = unicodedata.normalize("NFKD", text.casefold())
    return "".join(char for char in text if not unicodedata.combining(char))


def _render_pages(pdf_bytes: bytes) -> list[PageImage]:
    document = fitz.open(stream=pdf_bytes, filetype="pdf")
    if document.page_count == 0:
        raise ValueError("PDF has no pages")
    if document.page_count > 6:
        raise ValueError("PDF has too many pages; expected a two-page intake form")

    pages: list[PageImage] = []
    zoom = RENDER_DPI / 72.0
    matrix = fitz.Matrix(zoom, zoom)
    for page in document:
        # Reject pathological page sizes before allocating a large pixmap.
        if page.rect.width * page.rect.height > 20_000_000:
            raise ValueError("PDF page is too large to process safely")
        pixmap = page.get_pixmap(matrix=matrix, alpha=False)
        rgb = np.frombuffer(pixmap.samples, dtype=np.uint8).reshape(
            pixmap.height, pixmap.width, pixmap.n
        )[:, :, :3]
        gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        pages.append(
            PageImage(
                rgb=rgb,
                gray=gray,
                png_bytes=_to_png(rgb),
            )
        )
    document.close()
    return pages


def _to_png(rgb: np.ndarray) -> bytes:
    buffer = io.BytesIO()
    Image.fromarray(rgb).save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def _to_preview_jpeg(rgb: np.ndarray, max_width: int = 1400) -> tuple[str, int, int]:
    """Base64 JPEG preview of a rendered page, downscaled for the browser."""
    image = Image.fromarray(rgb)
    if image.width > max_width:
        ratio = max_width / image.width
        image = image.resize(
            (max_width, max(1, round(image.height * ratio))),
            Image.Resampling.LANCZOS,
        )
    buffer = io.BytesIO()
    image.convert("RGB").save(buffer, format="JPEG", quality=78, optimize=True)
    encoded = base64.b64encode(buffer.getvalue()).decode("ascii")
    return encoded, image.width, image.height


def _crop(gray: np.ndarray, region: tuple[float, float, float, float]) -> np.ndarray:
    height, width = gray.shape[:2]
    x0, y0, x1, y1 = region
    left = max(0, int(x0 * width))
    right = min(width, int(x1 * width))
    top = max(0, int(y0 * height))
    bottom = min(height, int(y1 * height))
    return gray[top:bottom, left:right]


def _ocr_with_confidence(image: np.ndarray, psm: int = 7) -> tuple[str, float]:
    """OCR a crop and report the mean word confidence Tesseract saw."""
    if image.size == 0:
        return "", 0.0
    image = cv2.resize(image, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)
    image = cv2.fastNlMeansDenoising(image, None, 10, 7, 21)
    _, binary = cv2.threshold(image, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    try:
        data = pytesseract.image_to_data(
            binary,
            lang=OCR_LANG,
            config=f"--oem 3 --psm {psm}",
            output_type=pytesseract.Output.DICT,
        )
    except (pytesseract.TesseractNotFoundError, pytesseract.TesseractError):
        return "", 0.0

    words = [word for word in data.get("text", []) if word.strip()]
    confidences = [
        float(confidence)
        for confidence in data.get("conf", [])
        if str(confidence).lstrip("-").isdigit() and float(confidence) >= 0
    ]
    text = " ".join(words).strip()
    mean_confidence = sum(confidences) / len(confidences) if confidences else 0.0
    return text, mean_confidence


def _ocr(image: np.ndarray, psm: int = 7) -> str:
    return _ocr_with_confidence(image, psm)[0]


def _ocr_page(gray: np.ndarray) -> str:
    try:
        return pytesseract.image_to_string(
            gray, lang=OCR_LANG, config="--oem 3 --psm 6"
        ).strip()
    except (pytesseract.TesseractNotFoundError, pytesseract.TesseractError):
        return ""


def _clean_value(value: str) -> str:
    value = re.sub(r"\s+", " ", value).strip(" .;,|\t")
    return value


def _parse_date(value: str) -> str | None:
    match = DATE_RE.search(value)
    if not match:
        return None
    day, month, year = (int(part) for part in match.groups())
    if not (1 <= day <= 31 and 1 <= month <= 12 and 1900 <= year <= 2100):
        return None
    return f"{year:04d}-{month:02d}-{day:02d}"


def _detect_is_intake(text: str, page_count: int) -> tuple[bool, float]:
    normalized = _normalize(text)
    # Several keywords exist with and without Czech diacritics; dedupe the
    # normalized forms so one printed label is never counted twice.
    keywords = {_normalize(keyword) for keyword in TITLE_KEYWORDS}
    hits = sum(1 for keyword in keywords if keyword in normalized)
    strong_title = any(_normalize(marker) in normalized for marker in TITLE_MARKERS)
    if page_count == 2:
        hits += 1

    confidence = min(1.0, hits / 6.0)
    # A printed title is decisive; otherwise require three independent labels.
    # This tolerates poor OCR on older/darker scans without accepting ordinary
    # letters that merely mention a name and a birth date.
    return strong_title or hits >= 3, confidence


def _dark_ratio(image: np.ndarray) -> float:
    if image.size == 0:
        return 0.0
    _, binary = cv2.threshold(image, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    kernel = np.ones((3, 3), np.uint8)
    binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, kernel)
    return float(np.count_nonzero(binary)) / float(binary.size)


def _marked(gray: np.ndarray, region: tuple[float, float, float, float]) -> bool:
    # A written "A"/"x" fills noticeably more of the cell than the printed
    # border alone. The threshold is deliberately conservative.
    return _dark_ratio(_crop(gray, region)) > 0.035


def _extract_checkbox_grid(
    gray: np.ndarray,
    columns: tuple[tuple[float, float], ...],
    rows: tuple[tuple[float, float], ...],
    keys: tuple[tuple[str, ...], ...],
    regions: list[dict[str, Any]] | None = None,
    page: int = 0,
) -> dict[str, bool]:
    result: dict[str, bool] = {}
    for row_index, row_keys in enumerate(keys):
        y0, y1 = rows[row_index]
        for column_index, key in enumerate(row_keys):
            x0, x1 = columns[column_index]
            marked = _marked(gray, (x0, y0, x1, y1))
            result[key] = marked
            if regions is not None:
                regions.append(
                    {
                        "field": key,
                        "page": page,
                        "x": round(x0, 4),
                        "y": round(y0, 4),
                        "width": round(x1 - x0, 4),
                        "height": round(y1 - y0, 4),
                        "text": "",
                        "confidence": None,
                        "value": "marked" if marked else "empty",
                        "warning": True,
                    }
                )
    return result


def _parse_field_value(key: str, raw: str) -> tuple[Any, bool]:
    """Return (accepted value, warning) for one page-1 field."""
    if key == "dateOfBirth":
        parsed = _parse_date(raw)
        return parsed, parsed is None

    if key == "email":
        match = EMAIL_RE.search(raw.replace(" ", ""))
        return (match.group(0).lower() if match else None), match is None

    if key == "phone":
        match = PHONE_RE.search(raw)
        return (re.sub(r"[^\d+]", "", match.group(0)) if match else None), match is None

    if key == "postalCode":
        match = re.search(r"\b(\d{3}\s?\d{2})\b", raw)
        return (match.group(1).replace(" ", "") if match else None), match is None

    if key == "insuranceCompany":
        match = INSURANCE_RE.search(raw)
        return (match.group(1) if match else None), match is None

    if key == "weight":
        match = re.search(r"\d{1,3}", raw)
        return match.group(0) if match else None, False

    if key == "height":
        match = re.search(r"\d{2,3}", raw)
        return match.group(0) if match else None, False

    if key in {"firstName", "lastName"}:
        cleaned = re.sub(r"[^\w\- ]", "", raw, flags=re.UNICODE).strip()
        if len(cleaned) >= 2 and any(char.isalpha() for char in cleaned):
            return cleaned, False
        return None, True

    if key == "address":
        if len(raw) >= 4 and any(char.isalpha() for char in raw):
            return raw, False
        return None, True

    return raw, False


def _parse_page1(
    gray: np.ndarray,
    fields: dict[str, Any],
    warnings: list[str],
    regions: list[dict[str, Any]],
) -> None:
    for key, region in PAGE1_FIELDS.items():
        raw, ocr_confidence = _ocr_with_confidence(_crop(gray, region))
        raw = _clean_value(raw)

        if not raw or ocr_confidence < 45:
            value, warning = None, True
        else:
            value, warning = _parse_field_value(key, raw)

        if value is not None:
            fields[key] = value
        if warning:
            warnings.append(key)

        x0, y0, x1, y1 = region
        regions.append(
            {
                "field": key,
                "page": 0,
                "x": round(x0, 4),
                "y": round(y0, 4),
                "width": round(x1 - x0, 4),
                "height": round(y1 - y0, 4),
                "text": raw,
                "confidence": round(ocr_confidence, 1),
                "value": value,
                "warning": warning,
            }
        )

    # Tick-box detection is intentionally conservative: a printed table
    # border otherwise reads as a mark. The optional vision model extracts
    # these; locally the digitizer confirms the checkboxes manually.
    _extract_checkbox_grid(gray, MED_COLUMNS, MED_ROWS, MED_GRID_KEYS, regions, page=0)
    _extract_checkbox_grid(
        gray, LIFESTYLE_COLUMNS, LIFESTYLE_ROWS, LIFESTYLE_GRID_KEYS, regions, page=0
    )
    warnings.append("checkboxes_review")


def _parse_page2(gray: np.ndarray, fields: dict[str, Any], warnings: list[str]) -> None:
    # Page 2 is almost entirely handwriting over printed labels. Tesseract
    # cannot read it reliably; keep the page out of the automatic result so
    # the digitizer never confirms fabricated text. A configured vision model
    # extracts this page instead.
    _ = gray, fields
    warnings.append("page2_handwriting_review")


def extract_intake_form(pdf_bytes: bytes) -> ExtractionResult:
    pages = _render_pages(pdf_bytes)
    text_layer = ""
    document = fitz.open(stream=pdf_bytes, filetype="pdf")
    for page in document:
        text_layer += "\n" + page.get_text("text")
    document.close()

    first_page_text = _ocr_page(pages[0].gray)

    # Every page is returned to the browser so the digitizer can compare the
    # original scan with what OCR/vision detected. The preview is an image
    # (not a PDF viewer overlay), so nothing covers the underlying scan.
    page_texts = [first_page_text]
    for page in pages[1:]:
        page_texts.append(_ocr_page(page.gray))

    preview_pages: list[dict[str, Any]] = []
    for index, page in enumerate(pages):
        encoded, width, height = _to_preview_jpeg(page.rgb)
        preview_pages.append(
            {"page": index, "width": width, "height": height, "image": encoded}
        )

    if len(text_layer.strip()) > 40:
        detection_text = text_layer
    else:
        detection_text = first_page_text
    is_intake, confidence = _detect_is_intake(detection_text, len(pages))

    warnings: list[str] = []
    if not is_intake:
        return ExtractionResult(
            is_intake_form=False,
            confidence=confidence,
            warnings=["not_intake_form"],
            text=detection_text[:12000],
            page_count=len(pages),
            page_texts=[text[:20000] for text in page_texts],
            preview_pages=preview_pages,
        )

    fields: dict[str, Any] = {}
    regions: list[dict[str, Any]] = []
    _parse_page1(pages[0].gray, fields, warnings, regions)
    if len(pages) > 1:
        _parse_page2(pages[1].gray, fields, warnings)

    # Page 1 printed header/date can still be read from a text layer even when
    # OCR of the handwriting is poor.
    if text_layer:
        date_match = DATE_RE.search(text_layer)
        if date_match and "dateOfBirth" not in fields:
            parsed = _parse_date(date_match.group(0))
            if parsed:
                fields["dateOfBirth"] = parsed

    warnings.append("handwriting_review")
    return ExtractionResult(
        is_intake_form=True,
        confidence=max(confidence, 0.6),
        fields=fields,
        warnings=sorted(set(warnings)),
        text=first_page_text[:12000],
        page_count=len(pages),
        page_texts=[text[:20000] for text in page_texts],
        preview_pages=preview_pages,
        regions=regions,
    )


def page_images_for_vision(pdf_bytes: bytes) -> list[bytes]:
    """PNG bytes for the optional vision-model extractor."""
    return [page.png_bytes for page in _render_pages(pdf_bytes)]
