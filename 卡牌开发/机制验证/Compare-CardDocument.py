"""Compare the current Word specification with a Git revision, without editing it."""
import argparse
import difflib
import hashlib
import io
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile

NS = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
W = "{" + NS["w"] + "}"


def paragraphs(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        root = ET.fromstring(archive.read("word/document.xml"))
    result = []
    for index, paragraph in enumerate(root.findall(".//w:body//w:p", NS), 1):
        runs = []
        for run in paragraph.findall(".//w:r", NS):
            text = "".join(node.text or "" if node.tag == W + "t" else
                           "\n" if node.tag in (W + "br", W + "cr") else
                           "\t" if node.tag == W + "tab" else ""
                           for node in run)
            if not text:
                continue
            properties = run.find("w:rPr", NS)
            formatting = {} if properties is None else {
                item.tag.removeprefix(W): {key.removeprefix(W): value for key, value in item.attrib.items()}
                for item in properties
                if item.tag in {W + "color", W + "highlight", W + "strike", W + "b", W + "i"}
            }
            runs.append({"text": text, "formatting": formatting})
        text = "".join(run["text"] for run in runs)
        if text.strip():
            result.append({"paragraph": index, "text": text, "runs": runs})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--document", default="晶塔世界角色.docx")
    parser.add_argument("--revision", default="HEAD")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    document = Path(args.document)
    current = document.read_bytes()
    previous = subprocess.run(["git", "show", args.revision + ":" + document.as_posix()],
                              check=True, stdout=subprocess.PIPE).stdout
    old, new = paragraphs(previous), paragraphs(current)
    diff = list(difflib.unified_diff([item["text"] for item in old],
                                  [item["text"] for item in new],
                                  fromfile=args.revision, tofile=str(document), lineterm=""))
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {"scope": "OOXML text and direct run formatting; no layout or inherited styles validation",
              "baseline_revision": args.revision,
              "baseline_sha256": hashlib.sha256(previous).hexdigest(),
              "source_sha256": hashlib.sha256(current).hexdigest(),
              "text_diff": diff, "baseline_paragraphs": old, "current_paragraphs": new}
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("\n".join(diff))
    print("Source SHA256:", report["source_sha256"])


if __name__ == "__main__":
    main()
