"""Prepare a cleaned copy; never overwrite live saves. Only zero-valued legacy entries on vanilla cards are removed."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

LEGACY = 'CardModel_crystal_coin_plays_this_run'

def clean_run(original):
    cleaned = copy.deepcopy(original)
    removed = []
    def visit(node, path='$'):
        if isinstance(node, dict):
            props = node.get('props')
            if isinstance(props, dict) and isinstance(props.get('ints'), list):
                retained = []
                for entry in props['ints']:
                    if isinstance(entry, dict) and entry.get('name') == LEGACY:
                        if set(entry) != {'name', 'value'} or type(entry['value']) is not int or entry['value'] != 0:
                            raise ValueError(f'Refusing to remove a nonzero/unexpected legacy entry at {path}')
                        if not isinstance(node.get('id'), str) or not node['id'].startswith('CARD.'):
                            raise ValueError(f'Refusing to remove a legacy entry from a non-vanilla card at {path}')
                        removed.append({'path': path + '.props.ints', 'card_id': node['id'], 'value': 0})
                    else:
                        retained.append(entry)
                props['ints'] = retained
            for key, value in node.items():
                visit(value, f'{path}.{key}')
        elif isinstance(node, list):
            for index, value in enumerate(node):
                visit(value, f'{path}[{index}]')
    visit(cleaned)
    # Never silently allow the legacy key elsewhere in the file.
    if LEGACY in json.dumps(cleaned, ensure_ascii=False):
        raise ValueError('Unexpected legacy occurrence remains')
    return cleaned, removed

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve() or args.output.exists():
        raise ValueError('Output must be a new separate file')
    data = json.loads(args.source.read_text(encoding='utf-8-sig'))
    cleaned, removed = clean_run(data)
    if not removed:
        raise ValueError('No matching legacy entries; nothing to repair')
    serialized = json.dumps(cleaned, ensure_ascii=False, indent=2) + '\n'
    assert json.loads(serialized) == cleaned
    args.output.write_text(serialized, encoding='utf-8', newline='\n')
    proof = {
        'status': 'prepared', 'legacy_property': LEGACY, 'removed_entries': len(removed),
        'all_removed_values_zero': True, 'all_removed_cards_vanilla': True,
        'source_sha256': hashlib.sha256(args.source.read_bytes()).hexdigest(),
        'cleaned_sha256': hashlib.sha256(args.output.read_bytes()).hexdigest(),
        'scope': 'only matching props.ints entries removed; all other JSON values preserved',
        'changes': removed,
    }
    args.report.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Prepared {args.output.name}: {len(removed)} zero-valued legacy entries removed; original untouched')

if __name__ == '__main__':
    main()
