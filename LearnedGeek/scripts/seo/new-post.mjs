#!/usr/bin/env node

/**
 * new-post.mjs — Scaffold a new blog post with SEO guidance
 *
 * Usage:
 *   node scripts/seo/new-post.mjs --title "My Post Title" --category tech --tags "dotnet,ef-core"
 *   node scripts/seo/new-post.mjs --title "My Post Title" --category tech --tags "dotnet,ef-core" --date 2026-04-01
 *   node scripts/seo/new-post.mjs --title "My Post Title" --category tech  # tags auto-suggested
 *
 * Creates:
 *   - Markdown file in Content/posts/
 *   - Entry in posts.json
 *   - Runs tag suggestions and link suggestions automatically
 */

import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { loadPosts, getMarkdownPath, CONTENT_ROOT, VALID_CATEGORIES } from './lib/posts.mjs';

const args = process.argv.slice(2);

function getArg(name) {
  const idx = args.indexOf(`--${name}`);
  return idx !== -1 ? args[idx + 1] : null;
}

function slugify(title) {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}

function buildTagVocabulary(posts) {
  const tagCounts = new Map();
  for (const post of posts) {
    for (const tag of (post.tags || [])) {
      const normalized = tag.toLowerCase();
      tagCounts.set(normalized, (tagCounts.get(normalized) || 0) + 1);
    }
  }
  return tagCounts;
}

function getTagOverlap(tagsA, tagsB) {
  const setB = new Set(tagsB.map(t => t.toLowerCase()));
  return tagsA.filter(t => setB.has(t.toLowerCase()));
}

async function main() {
  const title = getArg('title');
  const category = getArg('category');
  const tagsArg = getArg('tags');
  const dateArg = getArg('date');
  const description = getArg('description') || '';

  if (!title) {
    console.error('ERROR: --title is required.');
    console.error('Usage: node scripts/seo/new-post.mjs --title "My Post Title" --category tech [--tags "tag1,tag2"] [--date 2026-04-01] [--description "..."]');
    process.exit(1);
  }

  if (!category) {
    console.error(`ERROR: --category is required. Options: ${VALID_CATEGORIES.join(', ')}`);
    process.exit(1);
  }

  if (!VALID_CATEGORIES.includes(category.toLowerCase())) {
    console.error(`ERROR: Invalid category "${category}". Options: ${VALID_CATEGORIES.join(', ')}`);
    process.exit(1);
  }

  const slug = slugify(title);
  const tags = tagsArg ? tagsArg.split(',').map(t => t.trim().toLowerCase()) : [];
  const date = dateArg || new Date().toISOString().split('T')[0];

  console.log('\n=== New Post Scaffold ===\n');
  console.log(`  Title:    ${title}`);
  console.log(`  Slug:     ${slug}`);
  console.log(`  Category: ${category}`);
  console.log(`  Tags:     ${tags.length > 0 ? tags.join(', ') : '(none — see suggestions below)'}`);
  console.log(`  Date:     ${date}`);
  console.log('');

  // Load existing posts
  const postsJsonPath = join(CONTENT_ROOT, 'posts.json');
  const raw = await readFile(postsJsonPath, 'utf-8');
  const postsData = JSON.parse(raw);
  const existingPosts = postsData.posts;

  // Check for slug collision
  if (existingPosts.some(p => p.slug === slug)) {
    console.error(`ERROR: Slug "${slug}" already exists in posts.json.`);
    process.exit(1);
  }

  // --- Tag Suggestions ---
  const vocabulary = buildTagVocabulary(existingPosts);
  const tagSet = new Set(tags);

  if (tags.length === 0) {
    console.log('  No tags specified. Here are the most-used tags to choose from:\n');
    const sorted = [...vocabulary.entries()].sort((a, b) => b[1] - a[1]);
    for (const [tag, count] of sorted.slice(0, 20)) {
      console.log(`    ${tag} (${count} posts)`);
    }
    console.log('\n  Re-run with --tags "tag1,tag2,tag3" to assign tags.\n');
  } else {
    // Check for tags not in vocabulary
    const newTags = tags.filter(t => !vocabulary.has(t));
    const knownTags = tags.filter(t => vocabulary.has(t));

    if (knownTags.length > 0) {
      console.log(`  Existing tags: ${knownTags.join(', ')}`);
    }
    if (newTags.length > 0) {
      console.log(`  New tags (not yet used): ${newTags.join(', ')}`);

      // Suggest similar existing tags
      for (const newTag of newTags) {
        const similar = [...vocabulary.keys()].filter(existing =>
          existing.includes(newTag) || newTag.includes(existing) ||
          levenshteinClose(existing, newTag)
        );
        if (similar.length > 0) {
          console.log(`    Did you mean? ${similar.join(', ')} (instead of "${newTag}")`);
        }
      }
    }
    console.log('');
  }

  // --- Create markdown file ---
  const mdPath = getMarkdownPath(slug);
  const mdContent = `# ${title}\n\n<!-- TODO: Write your post here -->\n`;
  await writeFile(mdPath, mdContent, 'utf-8');
  console.log(`  Created: Content/posts/${slug}.md`);

  // --- Add to posts.json ---
  const newPost = {
    slug,
    title,
    description: description || `TODO: Write a 50-160 character description for SEO.`,
    category: category.toLowerCase(),
    tags,
    date: `${date}T00:00:00`,
    featured: false,
    image: `/img/posts/${slug}.svg`
  };

  // Insert at the top of the posts array (newest first)
  postsData.posts.unshift(newPost);

  await writeFile(postsJsonPath, JSON.stringify(postsData, null, 2), 'utf-8');
  console.log('  Updated: Content/posts.json\n');

  // --- Link Suggestions ---
  if (tags.length > 0) {
    console.log('=== Suggested Cross-Links ===\n');
    console.log('  Add these to your post where relevant:\n');

    const suggestions = [];
    for (const other of existingPosts) {
      const overlap = getTagOverlap(tags, other.tags || []);
      if (overlap.length >= 2) {
        suggestions.push({
          slug: other.slug,
          title: other.title,
          sharedTags: overlap,
          markdown: `[${other.title}](/Blog/Post/${other.slug})`
        });
      }
    }

    suggestions.sort((a, b) => b.sharedTags.length - a.sharedTags.length);

    if (suggestions.length === 0) {
      console.log('  No strong cross-link candidates found (need 2+ shared tags).\n');
    } else {
      for (const s of suggestions.slice(0, 8)) {
        console.log(`  Tags: ${s.sharedTags.join(', ')} (${s.sharedTags.length})`);
        console.log(`  Paste: ${s.markdown}`);
        console.log('');
      }
    }
  }

  // --- Reminders ---
  console.log('=== TODO ===\n');
  console.log(`  1. Write the post: Content/posts/${slug}.md`);
  if (!description) {
    console.log('  2. Add a description (50-160 chars) in posts.json');
  }
  console.log(`  ${description ? '2' : '3'}. Create the hero SVG: wwwroot/img/posts/${slug}.svg`);
  if (tags.length === 0) {
    console.log('  4. Add tags to the posts.json entry');
  }
  console.log('');
}

function levenshteinClose(a, b) {
  if (Math.abs(a.length - b.length) > 2) return false;
  let diff = 0;
  const shorter = a.length < b.length ? a : b;
  const longer = a.length >= b.length ? a : b;
  for (let i = 0; i < shorter.length; i++) {
    if (shorter[i] !== longer[i]) diff++;
    if (diff > 2) return false;
  }
  return diff + (longer.length - shorter.length) <= 2;
}

main().catch(err => {
  console.error('Post scaffolding failed:', err.message);
  process.exit(1);
});
