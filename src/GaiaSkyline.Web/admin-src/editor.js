// Admin rich-text editor — the ONLY entry bundled by esbuild into wwwroot/js/admin/editor.bundle.js
// (see ADR 0015). TipTap + ProseMirror are MIT, self-hosted so nothing loads from a CDN (the CSP forbids
// third-party scripts). This file is admin-only: it is referenced solely from the content editor view and
// never ships on public pages, so public bundles and Lighthouse budgets are untouched.
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Link from '@tiptap/extension-link';

// Keep the editor's capabilities aligned with the server-side sanitizer allowlist (bold, italic, links,
// lists, paragraphs) so what the owner sees is what survives the save. Marks/nodes that would be stripped
// are disabled here to avoid a confusing WYSIWYG mismatch.
function mount(container, options) {
  const opts = options || {};
  return new Editor({
    element: container,
    extensions: [
      StarterKit.configure({
        heading: false,
        blockquote: false,
        codeBlock: false,
        code: false,
        strike: false,
        horizontalRule: false,
      }),
      Link.configure({
        openOnClick: false,
        autolink: false,
        HTMLAttributes: { rel: 'noopener noreferrer', target: '_blank' },
      }),
    ],
    content: opts.content || '',
    onUpdate: ({ editor }) => {
      if (typeof opts.onUpdate === 'function') {
        opts.onUpdate(editor.getHTML());
      }
    },
  });
}

// A tiny, stable surface the hand-written glue (content-admin.js) drives. The returned Editor instance
// exposes chain()/commands for the toolbar; see https://tiptap.dev.
window.GaiaEditor = { mount };
