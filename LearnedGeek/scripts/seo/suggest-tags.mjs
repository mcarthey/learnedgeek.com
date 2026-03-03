#!/usr/bin/env node

/**
 * suggest-tags.mjs — Suggest tags from existing vocabulary
 *
 * Usage:
 *   node scripts/seo/suggest-tags.mjs --slug my-post           # Suggest tags for existing post based on content
 *   node scripts/seo/suggest-tags.mjs --title "My Post Title"  # Suggest tags based on title keywords
 *   node scripts/seo/suggest-tags.mjs --list                   # Show all existing tags with usage counts
 */

import { loadPosts, getMarkdownPath, fileExists } from './lib/posts.mjs';
import { readMarkdown } from './lib/markdown.mjs';

const args = process.argv.slice(2);
const slugIdx = args.indexOf('--slug');
const titleIdx = args.indexOf('--title');
const listMode = args.includes('--list');

function buildTagVocabulary(posts) {
  const tagCounts = new Map();
  for (const post of posts) {
    for (const tag of (post.tags || [])) {
      const normalized = tag.toLowerCase();
      if (!tagCounts.has(normalized)) {
        tagCounts.set(normalized, { tag: normalized, count: 0, posts: [] });
      }
      const entry = tagCounts.get(normalized);
      entry.count++;
      entry.posts.push(post.slug);
    }
  }
  return tagCounts;
}

function extractKeywords(text) {
  // Common stop words to filter out
  const stopWords = new Set([
    'a', 'an', 'the', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for',
    'of', 'with', 'by', 'from', 'is', 'it', 'as', 'be', 'was', 'are',
    'been', 'being', 'have', 'has', 'had', 'do', 'does', 'did', 'will',
    'would', 'could', 'should', 'may', 'might', 'can', 'shall', 'not',
    'no', 'nor', 'so', 'if', 'then', 'than', 'that', 'this', 'these',
    'those', 'what', 'which', 'who', 'whom', 'how', 'when', 'where',
    'why', 'all', 'each', 'every', 'both', 'few', 'more', 'most',
    'other', 'some', 'such', 'only', 'own', 'same', 'just', 'also',
    'very', 'about', 'above', 'after', 'again', 'any', 'because',
    'before', 'below', 'between', 'during', 'into', 'its', 'out',
    'over', 'through', 'under', 'until', 'up', 'while', 'you', 'your',
    'we', 'our', 'my', 'me', 'i', 'he', 'she', 'they', 'them', 'his',
    'her', 'their', 'here', 'there', 'like', 'get', 'got', 'make',
    'made', 'one', 'two', 'first', 'new', 'even', 'still', 'way',
    'use', 'used', 'using', 'know', 'need', 'want', 'see', 'look',
    'don', 'doesn', 'didn', 'won', 'let', 're', 've', 'll', 't', 's',
    'code', 'file', 'line', 'example', 'thing', 'work', 'something'
  ]);

  // Extract words, normalize, filter
  const words = text.toLowerCase()
    .replace(/[^a-z0-9\s-]/g, ' ')
    .split(/\s+/)
    .filter(w => w.length > 2 && !stopWords.has(w));

  // Count word frequency
  const freq = new Map();
  for (const word of words) {
    freq.set(word, (freq.get(word) || 0) + 1);
  }

  return freq;
}

function scoreTags(keywords, tagVocabulary) {
  const scores = [];

  for (const [tag, info] of tagVocabulary) {
    let score = 0;

    // Direct keyword match (tag appears as a word in content)
    const tagParts = tag.split('-');
    for (const part of tagParts) {
      if (keywords.has(part)) {
        score += keywords.get(part) * 2; // Weight by frequency
      }
    }

    // Full tag match in content
    if (keywords.has(tag)) {
      score += keywords.get(tag) * 3;
    }

    // Bonus for commonly-used tags (well-established vocabulary)
    if (info.count >= 5) score += 1;

    if (score > 0) {
      scores.push({ tag, score, usedIn: info.count });
    }
  }

  scores.sort((a, b) => b.score - a.score);
  return scores;
}

async function main() {
  const posts = await loadPosts({ includeScheduled: true });
  const vocabulary = buildTagVocabulary(posts);

  if (listMode) {
    console.log('=== Existing Tag Vocabulary ===\n');
    const sorted = [...vocabulary.values()].sort((a, b) => b.count - a.count);
    for (const { tag, count } of sorted) {
      const bar = '#'.repeat(Math.min(count, 30));
      console.log(`  ${tag.padEnd(35)} ${String(count).padStart(3)} ${bar}`);
    }
    console.log(`\n  ${vocabulary.size} unique tags across ${posts.length} posts.\n`);
    return;
  }

  let text = '';
  let currentTags = [];
  let label = '';

  if (slugIdx !== -1) {
    const slug = args[slugIdx + 1];
    const post = posts.find(p => p.slug === slug);

    if (post) {
      currentTags = post.tags || [];
      text = `${post.title} ${post.description} `;
      label = slug;
    }

    const mdPath = getMarkdownPath(slug);
    if (await fileExists(mdPath)) {
      text += await readMarkdown(mdPath);
    }

    if (!text.trim()) {
      console.error(`No content found for slug: ${slug}`);
      process.exit(1);
    }
  } else if (titleIdx !== -1) {
    text = args[titleIdx + 1];
    label = text;
  } else {
    console.error('Usage: --slug <slug>, --title "title text", or --list');
    process.exit(1);
  }

  const keywords = extractKeywords(text);
  const suggestions = scoreTags(keywords, vocabulary);
  const currentSet = new Set(currentTags.map(t => t.toLowerCase()));

  console.log(`\n=== Tag Suggestions for: ${label} ===\n`);

  if (currentTags.length > 0) {
    console.log(`  Current tags: ${currentTags.join(', ')}\n`);
  }

  const newSuggestions = suggestions.filter(s => !currentSet.has(s.tag));
  const confirmedTags = suggestions.filter(s => currentSet.has(s.tag));

  if (confirmedTags.length > 0) {
    console.log('  Confirmed (already assigned, good match):');
    for (const s of confirmedTags) {
      console.log(`    ${s.tag} (score: ${s.score}, used in ${s.usedIn} posts)`);
    }
    console.log('');
  }

  if (newSuggestions.length > 0) {
    console.log('  Suggested additions:');
    for (const s of newSuggestions.slice(0, 10)) {
      console.log(`    ${s.tag} (score: ${s.score}, used in ${s.usedIn} posts)`);
    }
    console.log('');
  } else {
    console.log('  No additional tags suggested.\n');
  }

  // Warn about tags not in vocabulary
  const unknownTags = currentTags.filter(t => !vocabulary.has(t.toLowerCase()));
  if (unknownTags.length > 0) {
    console.log('  New tags (not in existing vocabulary):');
    for (const tag of unknownTags) {
      console.log(`    ${tag} — will create a new tag category`);
    }
    console.log('');
  }
}

main().catch(err => {
  console.error('Tag suggestion failed:', err.message);
  process.exit(1);
});
