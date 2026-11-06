"""Exercise the site generator in an isolated tree; no web requests or publishing."""
from html.parser import HTMLParser
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.parse import urlsplit
import os
import shutil
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parent.parent


class Page(HTMLParser):
    def __init__(self, text):
        super().__init__()
        self.urls = []
        self.images = []
        self.videos = []
        self.ids = set()
        self.feed(text)

    def handle_starttag(self, tag, attributes):
        attributes = dict(attributes)
        if 'id' in attributes:
            self.ids.add(attributes['id'])
        self.urls += [attributes[key] for key in ('href', 'src') if key in attributes]
        if tag == 'img':
            self.images.append(attributes.get('src', ''))
        if tag == 'video':
            self.videos.append(attributes)


class SiteGenerationTests(unittest.TestCase):
    def test_rebuilding_uses_project_links_and_current_product_media(self):
        # A stale upstream download would replace Lokum instructions with a non-existent
        # package, and a stale video would falsely present the original app as Lokum.
        with TemporaryDirectory(prefix='lokum-site-generation-') as directory:
            tree = Path(directory)
            shutil.copytree(ROOT / 'docs', tree / 'docs')
            files = [
                'scripts/build-site.py',
                'Sources/LokumCore/FeatureCatalog.swift',
                'Sources/LokumCore/Models.swift',
                'Sources/LokumCore/Resources/Turkish.json',
                'Sources/LokumApp/ToolEditor.swift',
                'Sources/LokumApp/WheelFeedback.swift',
                'windows/Lokum.Windows/Theme.cs',
            ]
            for relative in files:
                target = tree / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / relative, target)
            subprocess.run([sys.executable, str(tree / 'scripts/build-site.py')], check=True,
                           env={**os.environ, 'PYTHONUTF8': '1'}, capture_output=True, text=True)
            for language in ('index.html', 'tr/index.html'):
                path = tree / 'docs' / language
                page = Page(path.read_text(encoding='utf-8'))
                with self.subTest(language=language):
                    self.assertNotIn('derived from', path.read_text(encoding='utf-8').casefold(),
                                     'The public product page must describe Lokum without origin marketing copy')
                    self.assertNotIn('kaynaklarından türetil', path.read_text(encoding='utf-8').casefold())
                    self.assertFalse([url for url in page.urls
                                      if urlsplit(url).netloc == 'github.com'
                                      and not urlsplit(url).path.startswith('/EnsarAslannn/Lokum')],
                                     'Product source links must lead to the Lokum repository')
                    self.assertFalse(page.videos, 'Upstream Mac recordings are not Lokum recordings')
                    self.assertIn('download', page.ids, 'Run instructions must survive regeneration')
                    self.assertTrue(any(url.endswith('README.md#çalıştırma') for url in page.urls),
                                    'The run action must lead to the local distribution instructions')
                    self.assertTrue(any(url.endswith('lokum-windows-light.png') for url in page.images))
                    self.assertTrue(any(url.endswith('lokum-windows-dark.png') for url in page.images))
                    for url in page.images:
                        parsed = urlsplit(url)
                        if not parsed.scheme:
                            self.assertTrue((path.parent / parsed.path).is_file(), url)


if __name__ == '__main__':
    unittest.main()
