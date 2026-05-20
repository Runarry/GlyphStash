import argparse
import json
import os
import sys
import tempfile
from pathlib import Path

from fontTools.merge import Merger
from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont

try:
    from fontTools.ttLib.scaleUpem import scale_upem
except Exception:  # pragma: no cover - depends on bundled fontTools version
    scale_upem = None


DETAIL_LIMIT = 500


def emit_progress(percent, stage, message):
    print(json.dumps({"percent": percent, "stage": stage, "message": message}, ensure_ascii=False), flush=True)


def issue(kind, severity, message, target=""):
    return {"kind": kind, "severity": severity, "message": message, "target": target}


def codepoint_label(codepoint):
    return f"U+{codepoint:04X}"


def expand_ranges(ranges):
    codepoints = []
    seen = set()
    for item in ranges:
        start = int(item["start"])
        end = int(item["end"])
        for codepoint in range(start, end + 1):
            if codepoint not in seen:
                seen.add(codepoint)
                codepoints.append(codepoint)
    return sorted(codepoints)


def load_font(path, target, issues):
    extension = Path(path).suffix.lower()
    if extension in {".ttc", ".otc", ".woff", ".woff2"}:
        issues.append(issue("UnsupportedFontKind", "Error", f"{target} 是 {extension[1:].upper()}，M4 v1 仅支持静态单字体 TTF/OTF。", target))
        return None
    if extension not in {".ttf", ".otf"}:
        issues.append(issue("UnsupportedFormat", "Error", f"{target} 格式不支持，M4 v1 仅支持 TTF/OTF。", target))
        return None
    if not os.path.isfile(path):
        issues.append(issue("MissingFile", "Error", f"{target} 文件不存在：{path}", target))
        return None
    try:
        font = TTFont(path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
    except Exception as exc:
        issues.append(issue("WorkerFailed", "Error", f"{target} 无法读取：{exc}", target))
        return None
    if "fvar" in font:
        issues.append(issue("VariableFont", "Error", f"{target} 是变量字体，M4 v1 不支持合并。", target))
    return font


def font_units_per_em(font):
    if font is not None and "head" in font:
        return int(font["head"].unitsPerEm)
    return 1000


def get_cmap(font):
    if font is None:
        return {}
    return font.getBestCmap() or {}


def create_conflicts(requested, base_cmap, supplemental_cmap):
    conflicts = []
    duplicate_count = 0
    missing_count = 0
    merge_count = 0
    coverage_count = 0
    for codepoint in requested:
        base_present = codepoint in base_cmap
        supplemental_present = codepoint in supplemental_cmap
        if supplemental_present:
            coverage_count += 1
        if base_present and supplemental_present:
            duplicate_count += 1
            decision = "SkipDuplicate"
            note = "基础字体已存在，默认跳过"
        elif not supplemental_present:
            missing_count += 1
            decision = "RecordMissing"
            note = "补充字体缺失，记录到报告"
        else:
            merge_count += 1
            decision = "Merge"
            note = "基础字体缺失，将从补充字体合并"

        if len(conflicts) < DETAIL_LIMIT:
            conflicts.append(
                {
                    "codePoint": codepoint,
                    "character": chr(codepoint),
                    "baseState": "Present" if base_present else "Missing",
                    "supplementalState": "Present" if supplemental_present else "Missing",
                    "defaultDecision": decision,
                    "note": note,
                }
            )

    return conflicts, coverage_count, merge_count, duplicate_count, missing_count


def rename_font(font, family_name):
    if not family_name or "name" not in font:
        return
    postscript_name = "".join(ch for ch in family_name if ch.isalnum() or ch in {"-", "_"})[:63] or "GlyphStashMerged"
    for record in font["name"].names:
        if record.nameID in {1, 4, 16}:
            set_name_string(record, family_name)
        elif record.nameID == 6:
            set_name_string(record, postscript_name)


def set_name_string(record, value):
    try:
        record.string = value.encode(record.getEncoding(), errors="replace")
    except Exception:
        record.string = value.encode("utf-16-be", errors="replace")


def subset_supplemental(supplemental_path, merge_codepoints, base_units, temp_dir):
    font = TTFont(supplemental_path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
    options = Options()
    options.ignore_missing_glyphs = True
    options.retain_gids = False
    subsetter = Subsetter(options=options)
    subsetter.populate(unicodes=merge_codepoints)
    subsetter.subset(font)
    if scale_upem is not None and "head" in font and int(font["head"].unitsPerEm) != base_units:
        scale_upem(font, base_units)
    subset_path = os.path.join(temp_dir, "supplemental-subset.ttf")
    font.save(subset_path)
    font.close()
    return subset_path


def perform_merge(base_path, supplemental_path, output_path, output_family_name, merge_codepoints):
    with tempfile.TemporaryDirectory(prefix="glyphstash-fonttools-") as temp_dir:
        base_font = TTFont(base_path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
        base_units = font_units_per_em(base_font)
        if merge_codepoints:
            subset_path = subset_supplemental(supplemental_path, merge_codepoints, base_units, temp_dir)
            base_font.close()
            merged = Merger().merge([base_path, subset_path])
        else:
            merged = base_font
        rename_font(merged, output_family_name)
        output_dir = os.path.dirname(output_path)
        if output_dir:
            os.makedirs(output_dir, exist_ok=True)
        merged.save(output_path)
        merged.close()


def analyze(request, dry_run):
    issues = []
    emit_progress(10, "读取", "正在读取输入字体...")
    base_font = load_font(request["baseFontPath"], "基础字体 A", issues)
    supplemental_font = load_font(request["supplementalFontPath"], "补充字体 B", issues)
    requested = expand_ranges(request["ranges"])

    if base_font is None or supplemental_font is None:
        return create_preview(issues, [], len(requested), 0, 0, 0, 0), []

    emit_progress(28, "预检查", "正在读取 Unicode cmap...")
    base_cmap = get_cmap(base_font)
    supplemental_cmap = get_cmap(supplemental_font)
    conflicts, coverage_count, merge_count, duplicate_count, missing_count = create_conflicts(requested, base_cmap, supplemental_cmap)
    merge_codepoints = sorted(codepoint for codepoint in requested if codepoint in supplemental_cmap and codepoint not in base_cmap)

    base_units = font_units_per_em(base_font)
    supplemental_units = font_units_per_em(supplemental_font)
    if base_units != supplemental_units:
        issues.append(issue("InvalidInput", "Info", f"字体 unitsPerEm 不一致：基础 {base_units}，补充 {supplemental_units}；worker 将按基础字体缩放补充字形。", "unitsPerEm"))

    base_font.close()
    supplemental_font.close()

    blocking = any(item["severity"] == "Error" for item in issues)
    if dry_run and not blocking and merge_codepoints:
        emit_progress(58, "Dry-run", "正在执行 fontTools 合并 dry-run...")
        with tempfile.TemporaryDirectory(prefix="glyphstash-fonttools-preview-") as temp_dir:
            preview_output = os.path.join(temp_dir, "preview.ttf")
            try:
                perform_merge(
                    request["baseFontPath"],
                    request["supplementalFontPath"],
                    preview_output,
                    request.get("outputFamilyName") or "GlyphStash Preview",
                    merge_codepoints,
                )
            except Exception as exc:
                issues.append(issue("OpenTypeLayoutConflict", "Error", f"fontTools dry-run 失败：{exc}", "fontTools merge"))

    if len(requested) > DETAIL_LIMIT:
        issues.append(issue("InvalidInput", "Info", f"冲突明细仅显示前 {DETAIL_LIMIT} 个码位，报告保留完整统计。", "冲突明细"))

    return create_preview(issues, conflicts, len(requested), coverage_count, merge_count, duplicate_count, missing_count), merge_codepoints


def create_preview(issues, conflicts, requested_count, coverage_count, merge_count, duplicate_count, missing_count):
    return {
        "issues": issues,
        "conflicts": conflicts,
        "requestedCodePointCount": requested_count,
        "supplementalCoverageCount": coverage_count,
        "mergeCodePointCount": merge_count,
        "duplicateCodePointCount": duplicate_count,
        "missingCodePointCount": missing_count,
    }


def write_response(path, preview, output_path="", error_message=""):
    with open(path, "w", encoding="utf-8") as handle:
        json.dump({"preview": preview, "outputPath": output_path, "errorMessage": error_message}, handle, ensure_ascii=False, indent=2)


def run(request):
    operation = request["operation"]
    if operation not in {"preview", "merge"}:
        raise RuntimeError(f"Unsupported operation: {operation}")

    preview, merge_codepoints = analyze(request, dry_run=True)
    blocking = any(item["severity"] == "Error" for item in preview["issues"])
    if operation == "preview" or blocking:
        emit_progress(100, "完成", "预览完成。")
        write_response(request["responsePath"], preview)
        return

    emit_progress(72, "合并", "正在写入输出字体...")
    perform_merge(
        request["baseFontPath"],
        request["supplementalFontPath"],
        request["outputPath"],
        request.get("outputFamilyName") or "GlyphStash Merged",
        merge_codepoints,
    )
    emit_progress(100, "完成", "输出字体已生成。")
    write_response(request["responsePath"], preview, request["outputPath"])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--request", required=True)
    args = parser.parse_args()
    try:
        with open(args.request, "r", encoding="utf-8") as handle:
            request = json.load(handle)
        run(request)
    except Exception as exc:
        response_path = None
        try:
            response_path = request.get("responsePath")
        except Exception:
            response_path = None
        if response_path:
            write_response(response_path, create_preview([issue("WorkerFailed", "Error", str(exc), "fontTools worker")], [], 0, 0, 0, 0, 0), error_message=str(exc))
        print(str(exc), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
