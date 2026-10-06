"""
ScripOx — CSV / Excel File Connector
Reads a local CSV or XLSX file and yields records.

Required param keys:
    file_path    : absolute path to the file
    delimiter    : CSV delimiter (default ',')
    encoding     : file encoding (default 'utf-8-sig')
    column_map   : dict mapping file columns → ScripOx fields
                   e.g. {"Company Name": "name", "Tel": "phone"}
"""

from __future__ import annotations
from typing import Any
import csv
from pathlib import Path
from loguru import logger

from connectors.base_connector import BaseConnector


class CsvConnector(BaseConnector):

    def get_name(self) -> str:
        return "csv_file"

    async def fetch(self) -> list[dict[str, Any]]:
        file_path  = Path(self.params["file_path"])
        delimiter  = self.params.get("delimiter", ",")
        encoding   = self.params.get("encoding", "utf-8-sig")
        column_map = self.params.get("column_map", {})

        if not file_path.exists():
            raise FileNotFoundError(f"File not found: {file_path}")

        results = []
        with open(file_path, mode="r", encoding=encoding) as f:
            reader = csv.DictReader(f, delimiter=delimiter)
            for row in reader:
                record: dict[str, Any] = {}

                if column_map:
                    for src_col, dst_field in column_map.items():
                        if src_col in row and row[src_col]:
                            record[dst_field] = self._safe_str(row[src_col])
                else:
                    # Auto-map: lowercase column names, replace spaces with _
                    for col in reader.fieldnames or []:
                        if col and row.get(col):
                            mapped = col.strip().lower().replace(" ", "_")
                            record[mapped] = self._safe_str(row[col])

                record["_raw"] = dict(row)
                results.append(record)
                self._fetched += 1

        self._log.info(f"Loaded {len(results)} records from {file_path.name}")
        return results
