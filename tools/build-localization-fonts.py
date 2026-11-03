"""Subset licensed, local Noto sources for the EEV catalogs; verify all glyphs."""
import argparse
import json
from pathlib import Path
from fontTools import subset
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--source-root', type=Path, required=True)
parser.add_argument('--japanese-source', type=Path, required=True)
parser.add_argument('--check-only', action='store_true')
parser.add_argument('--role-reference', type=Path)
args = parser.parse_args()
catalogs = {p.stem: json.loads(p.read_text(encoding='utf-8')) for p in (root/'Assets/Localization').glob('*.json')}
fonts = root/'Assets/Fonts'
special = {'Japanese','Korean','ChineseSimplified','ChineseTraditional','Arabic','Thai'}
def required(language):
    selected = [v for k,v in catalogs.items() if k not in special] if language == 'European' else [catalogs[language]]
    return {ord(c) for catalog in selected for text in catalog.values() for c in text if c not in '\n\r'}

for language, source in [('European','NotoSans-Regular.ttf'),('Korean','NotoSansCJKkr-Regular.otf'),
                         ('ChineseSimplified','NotoSansCJKsc-Regular.otf'),('ChineseTraditional','NotoSansCJKtc-Regular.otf')]:
    chars = required(language) | set(range(32,127))
    output = fonts/(language + Path(source).suffix)
    if not args.check_only:
        font = TTFont(args.source_root/source)
        missing = chars-set(font.getBestCmap())
        assert not missing, (language, sorted(missing))
        options = subset.Options()
        options.name_IDs=['*']; options.name_legacy=True; options.name_languages=['*']
        sub = subset.Subsetter(options=options); sub.populate(unicodes=chars); sub.subset(font)
        family = 'EliteEnemyVariants ' + language
        for record in font['name'].names:
            if record.nameID in [1,3,4,6,16]:
                record.string = (family.replace(' ','') if record.nameID == 6 else family).encode(record.getEncoding())
        if 'CFF ' in font:
            cff = font['CFF '].cff; cff.fontNames=[family.replace(' ','')]
            cff.topDictIndex[0].FamilyName=family; cff.topDictIndex[0].FullName=family
        font.save(output)
    assert not chars-set(TTFont(output).getBestCmap()), language
    print(language, len(chars), 'glyphs covered')

for language in ['Arabic','Thai']:
    available = set(TTFont(fonts/(language+'.ttf')).getBestCmap()) | set(TTFont(fonts/'European.ttf').getBestCmap())
    assert not required(language)-available, (language, sorted(required(language)-available))
    print(language, len(required(language)), 'glyphs covered')
# Japanese uses the same Checkpoint Revenge bundle as Roles. Latin glyphs use the game font.
japanese = {c for c in required('Japanese') if c >= 0x3000}
assert not japanese-set(TTFont(args.japanese_source).getBestCmap()), 'Japanese glyph coverage'
print('Japanese',len(japanese),'glyphs covered by Checkpoint Revenge')
print('PASS: all 21 language catalogs have glyph coverage.')
if args.role_reference:
    from fontTools.pens.recordingPen import DecomposingRecordingPen
    count = 0
    for name in ['European.ttf','Korean.otf','ChineseSimplified.otf','ChineseTraditional.otf']:
        original = TTFont(args.role_reference/'Assets/Fonts/Localization'/name)
        current = TTFont(fonts/name)
        before, after = original.getBestCmap(), current.getBestCmap()
        original_glyphs, current_glyphs = original.getGlyphSet(), current.getGlyphSet()
        common = set(before) & set(after)
        for char in common:
            # Subsetting renumbers unnamed component glyphs; compare decomposed outlines.
            left, right = DecomposingRecordingPen(original_glyphs), DecomposingRecordingPen(current_glyphs)
            original_glyphs[before[char]].draw(left); current_glyphs[after[char]].draw(right)
            assert left.value == right.value, (name, char, 'outline mismatch')
            assert original['hmtx'][before[char]] == current['hmtx'][after[char]], (name, char, 'metrics mismatch')
        count += len(common)
        print(name, len(common), 'shared glyph outlines and metrics identical to Roles')
    for name in ['CheckpointRevenge.bundle','Arabic.ttf','Thai.ttf']:
        reference = args.role_reference/'Assets/Fonts'/('' if name.endswith('.bundle') else 'Localization')/name
        assert reference.read_bytes() == (fonts/name).read_bytes(), (name,'file mismatch')
    print('PASS:',count,'common glyphs match Roles; Japanese/Arabic/Thai assets are byte-identical.')
