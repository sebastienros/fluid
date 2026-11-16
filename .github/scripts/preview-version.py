"""Prints the version of the preview packages built from main.

Reads release tags (v<major>.<minor>.<patch>[-<prerelease>]) on stdin and takes the
highest one by SemVer precedence, whatever branch it was released from:

- after a release like 3.0.0, previews are 3.0.1-preview.<run number>;
- after a prerelease like 3.1.0-beta.2, previews are 3.1.0-beta.2.preview.<run number>.

Previews always have a prerelease label, so they never collide with a release, and they
sort above the latest release or prerelease and below the next one. The run number keeps
them increasing.

    git ls-remote --tags --refs origin 'v*' | sed 's|.*refs/tags/||' | python3 preview-version.py <run number>
"""

import re
import sys

TAG = re.compile(r"^v(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$")


def precedence(version):
    major, minor, patch, prerelease = version
    # A release sorts above its prereleases. Prerelease identifiers compare numerically
    # when they are numbers, below text identifiers, and a longer list wins on a tie.
    labels = [] if prerelease is None else [
        (0, int(p), "") if p.isdigit() else (1, 0, p) for p in prerelease.split(".")
    ]
    return (major, minor, patch, prerelease is None, labels)


def main():
    if len(sys.argv) != 2 or not sys.argv[1].isdigit():
        sys.exit("usage: preview-version.py <run number> < tags")
    run = sys.argv[1]

    versions = []
    for line in sys.stdin:
        match = TAG.match(line.strip())
        if match:
            major, minor, patch, prerelease = match.groups()
            versions.append((int(major), int(minor), int(patch), prerelease))
    if not versions:
        sys.exit("no release tag found")

    major, minor, patch, prerelease = max(versions, key=precedence)
    if prerelease is None:
        print(f"{major}.{minor}.{patch + 1}-preview.{run}")
    else:
        print(f"{major}.{minor}.{patch}-{prerelease}.preview.{run}")


if __name__ == "__main__":
    main()
