from __future__ import annotations

import argparse
import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlretrieve
from zipfile import ZipFile

import numpy as np
import pandas as pd


logging.basicConfig(level="INFO", format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger("external-datasets")

DATASETS = {
    "uci_occupancy_detection": {
        "url": "https://archive.ics.uci.edu/static/public/357/occupancy+detection.zip",
        "room_id": 1,
        "note": "Office room temperature, humidity and CO2. No explicit HVAC telemetry.",
    },
    "uci_room_occupancy_estimation": {
        "url": "https://archive.ics.uci.edu/static/public/864/room+occupancy+estimation.zip",
        "room_id": 2,
        "note": "Multi-sensor room occupancy data. UCI notes no HVAC was used; humidity is imputed.",
    },
}
NORMALIZED_COLUMNS = [
    "source",
    "series_id",
    "room_id",
    "timestamp",
    "co2",
    "temperature",
    "humidity",
    "ventilation_power",
    "supply_ventilation_power",
    "exhaust_ventilation_power",
]


def default_datasets_root() -> Path:
    return Path(__file__).resolve().parents[1] / "datasets"


def ensure_dirs(root: Path) -> tuple[Path, Path, Path]:
    raw_dir = root / "raw"
    normalized_dir = root / "normalized"
    synthetic_dir = root / "synthetic"
    for directory in (raw_dir, normalized_dir, synthetic_dir):
        directory.mkdir(parents=True, exist_ok=True)
    return raw_dir, normalized_dir, synthetic_dir


def download_zip(name: str, raw_dir: Path, force: bool) -> Path:
    config = DATASETS[name]
    archive_path = raw_dir / f"{name}.zip"
    extract_dir = raw_dir / name
    if force or not archive_path.exists() or archive_path.stat().st_size == 0:
        logger.info("Downloading %s from %s", name, config["url"])
        urlretrieve(config["url"], archive_path)

    if force or not extract_dir.exists():
        extract_dir.mkdir(parents=True, exist_ok=True)
        with ZipFile(archive_path) as archive:
            archive.extractall(extract_dir)
    return extract_dir


def find_file(root: Path, filename: str) -> Path:
    matches = sorted(root.rglob(filename))
    if not matches:
        raise FileNotFoundError(f"{filename} was not found under {root}")
    return matches[0]


def base_frame(source: str, series_id: str, room_id: int, timestamp: pd.Series) -> pd.DataFrame:
    return pd.DataFrame(
        {
            "source": source,
            "series_id": series_id,
            "room_id": room_id,
            "timestamp": pd.to_datetime(timestamp, utc=True, errors="coerce"),
            "ventilation_power": 0.0,
            "supply_ventilation_power": 0.0,
            "exhaust_ventilation_power": 0.0,
        }
    )


def clean_normalized(df: pd.DataFrame) -> pd.DataFrame:
    result = df[NORMALIZED_COLUMNS].copy()
    for column in ("co2", "temperature", "humidity"):
        result[column] = pd.to_numeric(result[column], errors="coerce")
    result = result.dropna(subset=["timestamp", "co2", "temperature", "humidity"])
    result = result[
        result["co2"].between(300, 5000)
        & result["temperature"].between(-20, 60)
        & result["humidity"].between(0, 100)
    ]
    return result.sort_values(["series_id", "timestamp"]).reset_index(drop=True)


def normalize_uci_occupancy_detection(raw_root: Path) -> pd.DataFrame:
    frames: list[pd.DataFrame] = []
    config = DATASETS["uci_occupancy_detection"]
    for filename in ("datatraining.txt", "datatest.txt", "datatest2.txt"):
        path = find_file(raw_root, filename)
        raw = pd.read_csv(path)
        raw.columns = [column.strip() for column in raw.columns]
        out = base_frame(
            "uci_occupancy_detection",
            f"uci_occupancy_detection:{path.stem}",
            int(config["room_id"]),
            raw["date"],
        )
        out["co2"] = raw["CO2"]
        out["temperature"] = raw["Temperature"]
        out["humidity"] = raw["Humidity"]
        frames.append(out)
    return clean_normalized(pd.concat(frames, ignore_index=True))


def normalize_uci_room_occupancy_estimation(raw_root: Path) -> pd.DataFrame:
    config = DATASETS["uci_room_occupancy_estimation"]
    path = find_file(raw_root, "Occupancy_Estimation.csv")
    raw = pd.read_csv(path)
    raw.columns = [column.strip() for column in raw.columns]
    timestamp = raw["Date"].astype(str).str.strip() + " " + raw["Time"].astype(str).str.strip()
    out = base_frame(
        "uci_room_occupancy_estimation",
        "uci_room_occupancy_estimation:main",
        int(config["room_id"]),
        timestamp,
    )

    temp_columns = [column for column in raw.columns if column.endswith("_Temp")]
    out["temperature"] = raw[temp_columns].mean(axis=1)
    out["co2"] = raw["S5_CO2"]

    occupancy = pd.to_numeric(raw.get("Room_Occupancy_Count", 0), errors="coerce").fillna(0)
    hour = pd.to_datetime(timestamp, utc=True, errors="coerce").dt.hour.fillna(0)
    daily_cycle = np.sin((hour - 8) / 24 * 2 * np.pi)
    out["humidity"] = 42.0 + occupancy * 1.4 + daily_cycle * 3.0 - (out["temperature"] - 22.0) * 0.35
    out["humidity"] = out["humidity"].clip(30, 62)
    return clean_normalized(out)


def write_normalized(name: str, frame: pd.DataFrame, normalized_dir: Path) -> Path:
    path = normalized_dir / f"{name}.csv"
    frame.to_csv(path, index=False)
    logger.info("Wrote %s normalized rows to %s", len(frame), path)
    return path


def download_and_normalize(root: Path, force: bool = False) -> dict:
    raw_dir, normalized_dir, _synthetic_dir = ensure_dirs(root)
    occupancy_root = download_zip("uci_occupancy_detection", raw_dir, force)
    room_occupancy_root = download_zip("uci_room_occupancy_estimation", raw_dir, force)

    frames = {
        "uci_occupancy_detection": normalize_uci_occupancy_detection(occupancy_root),
        "uci_room_occupancy_estimation": normalize_uci_room_occupancy_estimation(room_occupancy_root),
    }
    normalized_files = {
        name: str(write_normalized(name, frame, normalized_dir))
        for name, frame in frames.items()
    }

    merged = pd.concat(frames.values(), ignore_index=True).sort_values(["series_id", "timestamp"])
    merged_path = normalized_dir / "airsense_external_training.csv"
    merged.to_csv(merged_path, index=False)
    normalized_files["airsense_external_training"] = str(merged_path)

    manifest = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "normalized_schema": NORMALIZED_COLUMNS,
        "datasets": {
            name: {
                "url": DATASETS[name]["url"],
                "rows": int(len(frame)),
                "series": sorted(str(value) for value in frame["series_id"].unique()),
                "note": DATASETS[name]["note"],
                "normalized_file": normalized_files[name],
            }
            for name, frame in frames.items()
        },
        "merged": {
            "rows": int(len(merged)),
            "file": str(merged_path),
        },
    }
    manifest_path = normalized_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    logger.info("Wrote dataset manifest to %s", manifest_path)
    return manifest


def export_synthetic_snapshot(root: Path, label: str | None = None) -> dict:
    _raw_dir, _normalized_dir, synthetic_dir = ensure_dirs(root)
    from .training import load_telemetry

    frame = load_telemetry()
    if frame.empty:
        raise RuntimeError("No AirSense DB telemetry available for synthetic snapshot export")

    suffix = label or datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S")
    path = synthetic_dir / f"airsense_synthetic_snapshot_{suffix}.csv"
    frame.to_csv(path, index=False)
    summary = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "rows": int(len(frame)),
        "series": int(frame["series_id"].nunique()),
        "sources": sorted(str(value) for value in frame["source"].unique()),
        "file": str(path),
    }
    summary_path = synthetic_dir / f"airsense_synthetic_snapshot_{suffix}.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    logger.info("Exported %s synthetic rows to %s", len(frame), path)
    return summary


def main() -> None:
    parser = argparse.ArgumentParser(description="Download and normalize external AirSense training datasets.")
    parser.add_argument(
        "--datasets-root",
        type=Path,
        default=default_datasets_root(),
        help="Root directory containing raw, normalized and synthetic dataset folders.",
    )
    parser.add_argument("--force-download", action="store_true", help="Re-download and re-extract raw archives.")
    subparsers = parser.add_subparsers(dest="command")
    subparsers.add_parser("download-normalize")
    synthetic_parser = subparsers.add_parser("export-synthetic")
    synthetic_parser.add_argument("--label", default=None)
    args = parser.parse_args()

    command = args.command or "download-normalize"
    if command == "download-normalize":
        print(json.dumps(download_and_normalize(args.datasets_root, args.force_download), indent=2))
    elif command == "export-synthetic":
        print(json.dumps(export_synthetic_snapshot(args.datasets_root, args.label), indent=2))
    else:
        raise SystemExit(f"Unknown command: {command}")


if __name__ == "__main__":
    main()
