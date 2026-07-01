import argparse
import json
import os
import sys
import tempfile
from pathlib import Path

from fontTools.merge import Merger, Options as MergeOptions
from fontTools.subset import Options as SubsetOptions, Subsetter
from fontTools.ttLib import TTFont

try:
    from fontTools.ttLib.scaleUpem import scale_upem
except Exception:  # pragma: no cover - depends on bundled fontTools version
    scale_upem = None


DETAIL_LIMIT = 500
SUPPORTED_OUTLINE_MESSAGE = "M4 v1 仅支持静态 TrueType/glyf 轮廓字体合并；请优先使用静态 TTF(glyf) 版本，暂不要混合 CFF/CFF2 OTF。"
INCOMPATIBLE_TABLE_MESSAGE = "输入字体的 OpenType 表结构不兼容，fontTools 要求部分字段在两个字体中一致，但当前组合存在缺失值或不一致值；M4 v1 不能安全合并这组字体。请尝试使用同一家族、同格式的静态 TTF(glyf)，或先用专业字体工具统一表结构后再合并。"
SINGLE_SIDED_OPTIONAL_DROP_TABLES = frozenset({"vhea", "vmtx", "VDMX", "hdmx", "LTSH", "PCLT"})


def emit_progress(percent, stage, message):
    print(json.dumps({"percent": percent, "stage": stage, "message": message}, ensure_ascii=False), flush=True)


def issue(kind, severity, message, target=""):
    return {"kind": kind, "severity": severity, "message": message, "target": target}


def codepoint_label(codepoint):
    return f"U+{codepoint:04X}"


def normalize_merge_mode(value):
    normalized = str(value or "Supplement").strip().lower()
    if normalized in {"overwrite", "1", "覆盖"}:
        return "Overwrite"
    return "Supplement"


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
        issues.append(issue("UnsupportedFontKind", "Error", f"{target} 是 {extension[1:].upper()}，M4 v1 仅支持静态单字体 TTF/OTF；合并阶段仅支持静态 TrueType/glyf 轮廓字体。", target))
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


def create_outline_profile(font):
    has_glyf = "glyf" in font
    has_cff = "CFF " in font
    has_cff2 = "CFF2" in font
    is_cid_keyed_cff = False
    if has_cff:
        try:
            top_dict = font["CFF "].cff.topDictIndex[0]
            is_cid_keyed_cff = any(getattr(top_dict, name, None) is not None for name in ("ROS", "FDArray", "FDSelect"))
        except Exception:
            is_cid_keyed_cff = True

    if has_glyf and not has_cff and not has_cff2:
        outline_kind = "glyf"
        label = "TrueType/glyf"
    elif has_cff:
        outline_kind = "cff"
        label = "CID-keyed CFF" if is_cid_keyed_cff else "CFF"
    elif has_cff2:
        outline_kind = "cff2"
        label = "CFF2"
    else:
        outline_kind = "unknown"
        label = "未知轮廓"

    return {
        "outlineKind": outline_kind,
        "label": label,
        "isCidKeyedCff": is_cid_keyed_cff,
        "isVariable": "fvar" in font,
    }


def validate_outline_profile(profile, target, issues):
    if profile["outlineKind"] == "glyf":
        return
    issues.append(
        issue(
            "UnsupportedFontKind",
            "Error",
            f"{target} 使用 {profile['label']} 轮廓，{SUPPORTED_OUTLINE_MESSAGE}",
            target,
        )
    )


def validate_outline_pair(base_profile, supplemental_profile, issues):
    outline_kinds = {base_profile["outlineKind"], supplemental_profile["outlineKind"]}
    if "glyf" in outline_kinds and ("cff" in outline_kinds or "cff2" in outline_kinds):
        issues.append(
            issue(
                "UnsupportedFontKind",
                "Error",
                f"基础字体 A 与补充字体 B 的轮廓类型不同（基础 {base_profile['label']}，补充 {supplemental_profile['label']}），{SUPPORTED_OUTLINE_MESSAGE}",
                "字体轮廓",
            )
        )


def font_units_per_em(font):
    if font is not None and "head" in font:
        return int(font["head"].unitsPerEm)
    return 1000


def get_cmap(font):
    if font is None:
        return {}
    return font.getBestCmap() or {}


def create_conflicts(requested, base_cmap, supplemental_cmap, merge_mode):
    conflicts = []
    duplicate_count = 0
    missing_count = 0
    merge_count = 0
    coverage_count = 0
    overwritten_count = 0
    for codepoint in requested:
        base_present = codepoint in base_cmap
        supplemental_present = codepoint in supplemental_cmap
        if supplemental_present:
            coverage_count += 1
        if base_present and supplemental_present:
            duplicate_count += 1
            if merge_mode == "Overwrite":
                overwritten_count += 1
                decision = "Overwrite"
                note = "基础字体已存在，覆盖模式将使用补充字体替换"
            else:
                decision = "SkipDuplicate"
                note = "基础字体已存在，补全模式默认跳过"
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

    return conflicts, coverage_count, merge_count, duplicate_count, missing_count, overwritten_count


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


def subset_font(input_path, codepoints, output_path, target_units=None):
    font = TTFont(input_path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
    options = SubsetOptions()
    options.ignore_missing_glyphs = True
    options.retain_gids = False
    subsetter = Subsetter(options=options)
    subsetter.populate(unicodes=codepoints)
    subsetter.subset(font)
    if target_units is not None and scale_upem is not None and "head" in font and int(font["head"].unitsPerEm) != target_units:
        scale_upem(font, target_units)
    font.save(output_path)
    font.close()
    return output_path


def subset_supplemental(supplemental_path, merge_codepoints, base_units, temp_dir):
    subset_path = os.path.join(temp_dir, "supplemental-subset.ttf")
    return subset_font(supplemental_path, merge_codepoints, subset_path, base_units)


def subset_base_for_overwrite(base_path, overwritten_codepoints, temp_dir):
    font = TTFont(base_path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
    base_cmap = get_cmap(font)
    font.close()
    keep_codepoints = sorted(codepoint for codepoint in base_cmap if codepoint not in set(overwritten_codepoints))
    subset_path = os.path.join(temp_dir, "base-without-overwrites.ttf")
    return subset_font(base_path, keep_codepoints, subset_path)


def create_optional_merge_drop_tables(base_tags, supplemental_tags):
    single_sided_tags = set(base_tags).symmetric_difference(set(supplemental_tags))
    return sorted(single_sided_tags.intersection(SINGLE_SIDED_OPTIONAL_DROP_TABLES))


def read_font_table_tags(path):
    font = TTFont(path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
    try:
        return set(font.keys()).difference({"GlyphOrder"})
    finally:
        font.close()


def merge_font_paths(base_input_path, supplemental_input_path):
    drop_tables = create_optional_merge_drop_tables(
        read_font_table_tags(base_input_path),
        read_font_table_tags(supplemental_input_path),
    )
    merged = Merger(MergeOptions(drop_tables=drop_tables)).merge([base_input_path, supplemental_input_path])
    return merged, drop_tables


def perform_merge(base_path, supplemental_path, output_path, output_family_name, merge_codepoints, merge_mode):
    dropped_tables = []
    with tempfile.TemporaryDirectory(prefix="glyphstash-fonttools-") as temp_dir:
        base_font = TTFont(base_path, recalcBBoxes=False, recalcTimestamp=False, lazy=False)
        base_units = font_units_per_em(base_font)
        if merge_codepoints:
            subset_path = subset_supplemental(supplemental_path, merge_codepoints, base_units, temp_dir)
            base_input_path = base_path
            if merge_mode == "Overwrite":
                base_input_path = subset_base_for_overwrite(base_path, merge_codepoints, temp_dir)
            base_font.close()
            merged, dropped_tables = merge_font_paths(base_input_path, subset_path)
        else:
            merged = base_font
        rename_font(merged, output_family_name)
        output_dir = os.path.dirname(output_path)
        if output_dir:
            os.makedirs(output_dir, exist_ok=True)
        merged.save(output_path)
        merged.close()
    return dropped_tables


def create_merge_failure_issue(exc):
    message = str(exc)
    normalized = message.lower().replace("notlmplemented", "notimplemented")
    if (
        "notimplementedtype" in normalized and "cff" in normalized
        or "cid-keyed cff" in normalized
        or "cff tables" in normalized
    ):
        return issue("UnsupportedFontKind", "Error", f"fontTools dry-run 失败：输入字体包含 CFF/CFF2 或混合轮廓，{SUPPORTED_OUTLINE_MESSAGE}", "fontTools merge")
    if "items to be equal" in normalized or "notimplemented" in normalized or "not supported between instances" in normalized:
        return issue("OpenTypeLayoutConflict", "Error", f"fontTools dry-run 失败：{INCOMPATIBLE_TABLE_MESSAGE} 原始错误：{message}", "fontTools merge")
    return issue("OpenTypeLayoutConflict", "Error", f"fontTools dry-run 失败：{message}", "fontTools merge")


def create_dropped_optional_tables_issue(dropped_tables):
    table_list = ", ".join(dropped_tables)
    return issue(
        "OpenTypeLayoutConflict",
        "Info",
        f"fontTools 合并时已跳过单边存在的可选表：{table_list}。这些表通常用于垂直排版或设备指标；为避免表结构冲突，本次仅合并字形轮廓、cmap 与基础 metrics。",
        "fontTools merge",
    )


def analyze(request, dry_run):
    issues = []
    merge_mode = normalize_merge_mode(request.get("mergeMode"))
    emit_progress(10, "读取", "正在读取输入字体...")
    base_font = load_font(request["baseFontPath"], "基础字体 A", issues)
    supplemental_font = load_font(request["supplementalFontPath"], "补充字体 B", issues)
    requested = expand_ranges(request["ranges"])

    if base_font is None or supplemental_font is None:
        return create_preview(issues, [], len(requested), 0, 0, 0, 0, 0), []

    emit_progress(28, "预检查", "正在读取 Unicode cmap...")
    base_profile = create_outline_profile(base_font)
    supplemental_profile = create_outline_profile(supplemental_font)
    validate_outline_profile(base_profile, "基础字体 A", issues)
    validate_outline_profile(supplemental_profile, "补充字体 B", issues)
    validate_outline_pair(base_profile, supplemental_profile, issues)

    base_cmap = get_cmap(base_font)
    supplemental_cmap = get_cmap(supplemental_font)
    conflicts, coverage_count, merge_count, duplicate_count, missing_count, overwritten_count = create_conflicts(
        requested,
        base_cmap,
        supplemental_cmap,
        merge_mode,
    )
    if merge_mode == "Overwrite":
        merge_codepoints = sorted(codepoint for codepoint in requested if codepoint in supplemental_cmap)
    else:
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
                dropped_tables = perform_merge(
                    request["baseFontPath"],
                    request["supplementalFontPath"],
                    preview_output,
                    request.get("outputFamilyName") or "GlyphStash Preview",
                    merge_codepoints,
                    merge_mode,
                )
                if dropped_tables:
                    issues.append(create_dropped_optional_tables_issue(dropped_tables))
            except Exception as exc:
                issues.append(create_merge_failure_issue(exc))

    if len(requested) > DETAIL_LIMIT:
        issues.append(issue("InvalidInput", "Info", f"冲突明细仅显示前 {DETAIL_LIMIT} 个码位，报告保留完整统计。", "冲突明细"))

    return create_preview(
        issues,
        conflicts,
        len(requested),
        coverage_count,
        merge_count,
        duplicate_count,
        missing_count,
        overwritten_count,
    ), merge_codepoints


def create_preview(issues, conflicts, requested_count, coverage_count, merge_count, duplicate_count, missing_count, overwritten_count):
    return {
        "issues": issues,
        "conflicts": conflicts,
        "requestedCodePointCount": requested_count,
        "supplementalCoverageCount": coverage_count,
        "mergeCodePointCount": merge_count,
        "duplicateCodePointCount": duplicate_count,
        "missingCodePointCount": missing_count,
        "overwrittenCodePointCount": overwritten_count,
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
        normalize_merge_mode(request.get("mergeMode")),
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
            write_response(response_path, create_preview([issue("WorkerFailed", "Error", str(exc), "fontTools worker")], [], 0, 0, 0, 0, 0, 0), error_message=str(exc))
        print(str(exc), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
