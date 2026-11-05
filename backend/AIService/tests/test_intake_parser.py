import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.intake_parser import ExtractionResult, _detect_is_intake, _parse_date


def test_parse_czech_date():
    assert _parse_date("29.11.1970") == "1970-11-29"
    assert _parse_date("5/6/2024") == "2024-06-05"
    assert _parse_date("not a date") is None


def test_extraction_result_uses_camel_case():
    result = ExtractionResult(is_intake_form=True, confidence=0.9)
    payload = result.as_dict()
    assert payload["isIntakeForm"] is True
    assert payload["confidence"] == 0.9
    assert payload["pageCount"] == 0
    assert payload["pageTexts"] == []
    assert payload["previewPages"] == []
    assert payload["regions"] == []


def test_detection_accepts_the_printed_title_even_with_poor_ocr():
    # A single page with a readable title is enough; the rest of the scan may
    # be badly OCR'd and no second page bonus is available.
    is_intake, confidence = _detect_is_intake("VSTUPNI FORMULAR KLIENTA", 1)
    assert is_intake is True
    assert confidence > 0


def test_detection_accepts_three_characteristic_labels_without_title():
    text = "Prijmeni: ... Datum narozeni: ... Zdravotni pojistovna: ..."
    is_intake, _ = _detect_is_intake(text, 1)
    assert is_intake is True


def test_detection_rejects_a_generic_letter_with_a_name_and_date():
    is_intake, _ = _detect_is_intake("Jmeno: Jan Datum: 1. 2. 2024", 1)
    assert is_intake is False
