# Akari Tool — website

Static site for [Akari Tool](https://github.com/isleap9/Akari-Tool). No build step, no dependencies, no hosting cost.

## Files

- `index.html` — home
- `features.html` — all 13 modules
- `docs.html` — install, first run, troubleshooting
- `changelog.html` — reads GitHub Releases live
- `404.html` — not-found page

Each page is fully self-contained: images, fonts fallbacks and scripts are inlined, so the files work from any folder, any host, or straight off disk.

## Publishing on GitHub Pages (free)

1. Push these files to the repo — either the root of a `gh-pages` branch, or a `/docs` folder on `main`.
2. Repo → **Settings → Pages** → Source: *Deploy from a branch* → pick that branch/folder.
3. Wait a minute; the site appears at `https://isleap9.github.io/Akari-Tool/`.

`404.html` is picked up automatically by GitHub Pages.

## Live data

The star count, download button and changelog are fetched from the public GitHub API at page load — releases never need to be copied into the site by hand. If the API is unreachable the pages fall back to static text.

## Editing

Copy is plain HTML inside each file. To change the accent colour, edit the `--red` value in the `:root` and `html[data-theme="light"]` blocks near the top of each page.
