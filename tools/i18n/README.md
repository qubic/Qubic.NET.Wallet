# UI translations

`strings.py` is the single source of truth for every translatable UI string
(key → { en, es, fr, de, ru, zh }). English (`en`) is the source text.

Regenerate the six `.resx` files after editing `strings.py`:

    python3 tools/i18n/gen.py

Rule: every change that adds or alters a UI string must fill in **all six languages**
for that key in `strings.py` — never ship English-only. `gen.py` reports any gaps.

In markup, inject `IStringLocalizer<AppStrings> Loc` and use `@Loc["Key"]`.
Keys are prefixed by area: `Nav_`, `Shell_`, `Home_`, `Send_`, `Receive_`, `VO_`, `Common_`, …
