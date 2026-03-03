#!/usr/bin/env node

/**
 * suggest-links.mjs — Generate paste-ready internal link snippets
 *
 * Usage:
 *   node scripts/seo/suggest-links.mjs --slug my-post     # Suggestions for one post
 *   node scripts/seo/suggest-links.mjs                     # Top suggestions across all posts
 *   node scripts/seo/suggest-links.mjs --min-tags 3        # Require 3+ shared tags
 */

import { loadPosts, getMarkdownPath, fileExists } from './lib/posts.mjs';
import { readMarkdown, extractInternalLinks } from './lib/markdown.mjs';

const args = process.argv.slice(2);
const slugIdx = args.indexOf('--slug');
const filterSlug = slugIdx !== -1 ? args[slugIdx + 1] : null;
const minTagsIdx = args.indexOf('--min-tags');
const minTags = minTagsIdx !== -1 ? parseInt(args[minTagsIdx + 1], 10) : 2;

async function getExistingLinks(slug) {
  const mdPath = getMarkdownPath(slug);
  if (!(await fileExists(mdPath))) return new Set();
  const content = await readMarkdown(mdPath);
  return new Set(extractInternalLinks(content).map(l => l.slug));
}

function getTagOverlap(tagsA, tagsB) {
  const setB = new Set(tagsB.map(t => t.toLowerCase()));
  return tagsA.filter(t => setB.has(t.toLowerCase()));
}

async function main() {
  const posts = await loadPosts({ includeScheduled: true });
  const postMap = new Map(posts.map(p => [p.slug, p]));

  const postsToCheck = filterSlug
    ? posts.filter(p => p.slug === filterSlug)
    : posts;

  if (filterSlug && postsToCheck.length === 0) {
    console.error(`Post not found: ${filterSlug}`);
    process.exit(1);
  }

  let totalSuggestions = 0;

  for (const post of postsToCheck) {
    const existingLinks = await getExistingLinks(post.slug);
    const suggestions = [];

    for (const other of posts) {
      if (other.slug === post.slug) continue;
      if (existingLinks.has(other.slug)) continue;

      const overlap = getTagOverlap(post.tags || [], other.tags || []);
      if (overlap.length >= minTags) {
        suggestions.push({
          slug: other.slug,
          title: other.title,
          sharedTags: overlap,
          markdown: `[${other.title}](/Blog/Post/${other.slug})`
        });
      }
    }

    if (suggestions.length === 0) continue;

    suggestions.sort((a, b) => b.sharedTags.length - a.sharedTags.length);
    totalSuggestions += suggestions.length;

    console.log(`\n--- ${post.slug} ---`);
    console.log(`  "${post.title}"\n`);

    for (const s of suggestions.slice(0, 5)) {
      console.log(`  Tags: ${s.sharedTags.join(', ')} (${s.sharedTags.length})`);
      console.log(`  Paste: ${s.markdown}`);
      console.log('');
    }

    if (suggestions.length > 5) {
      console.log(`  ... ${suggestions.length - 5} more (use --min-tags ${minTags + 1} to narrow)\n`);
    }
  }

  if (totalSuggestions === 0) {
    console.log('\nNo link suggestions found.\n');
  } else {
    console.log(`\n${totalSuggestions} total suggestions across ${postsToCheck.length} post(s).\n`);
  }
}

main().catch(err => {
  console.error('Link suggestion failed:', err.message);
  process.exit(1);
});
