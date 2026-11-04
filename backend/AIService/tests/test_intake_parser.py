import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.intake_parser import ExtractionResult, _parse_date


def test_parse_czech_date():
    assert _parse_date("29.11.1970") == "1970-11-29"
    assert _parse_date("5/6/2024") == "2024-06-05"
    assert _parse_date("not a date") is None


def test_extraction_result_uses_camel_case():
    result = ExtractionResult(is_intake_form=True, confidence=0.9)
    payload = result.as_dict()
    assert payload["isIntakeForm"] is True
    assert payload["confidence"] == 0.9
