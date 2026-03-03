#!/usr/bin/env node

/**
 * describe.mjs — Flag and help fix post descriptions outside SEO range
 *
 * Usage:
 *   node scripts/seo/describe.mjs                # Show all posts with description issues
 *   node scripts/seo/describe.mjs --slug my-post # Show one post's description analysis
 *   node scripts/seo/describe.mjs --long-only    # Only show descriptions that are too long
 *   node scripts/seo/describe.mjs --short-only   # Only show descriptions that are too short
 */

import { loadPosts } from './lib/posts.mjs';

const MIN_LENGTH = 50;
const MAX_LENGTH = 160;

const args = process.argv.slice(2);
const slugIdx = args.indexOf('--slug');
const filterSlug = slugIdx !== -1 ? args[slugIdx + 1] : null;
const longOnly = args.includes('--long-only');
const shortOnly = args.includes('--short-only');

function truncateAt(text, maxLen) {
  if (text.length <= maxLen) return text;
  // Find the last space before the cutoff to avoid mid-word breaks
  const cutoff = text.lastIndexOf(' ', maxLen);
  const breakPoint = cutoff > maxLen - 30 ? cutoff : maxLen;
  return text.substring(0, breakPoint);
}

async function main() {
  const posts = await loadPosts({ includeScheduled: true });

  let postsToCheck = filterSlug
    ? posts.filter(p => p.slug === filterSlug)
    : posts;

  if (filterSlug && postsToCheck.length === 0) {
    console.error(`Post not found: ${filterSlug}`);
    process.exit(1);
  }

  const tooLong = [];
  const tooShort = [];
  const justRight = [];

  for (const post of postsToCheck) {
    const len = (post.description || '').length;
    if (len > MAX_LENGTH) {
      tooLong.push(post);
    } else if (len < MIN_LENGTH) {
      tooShort.push(post);
    } else {
      justRight.push(post);
    }
  }

  if (!shortOnly && tooLong.length > 0) {
    console.log(`=== Too Long (>${MAX_LENGTH} chars) — ${tooLong.length} posts ===\n`);
    console.log('Google truncates these in search results. The "|" below marks the 160-char cutoff.\n');

    for (const post of tooLong) {
      const len = post.description.length;
      const visible = truncateAt(post.description, MAX_LENGTH);
      const truncated = post.description.substring(visible.length);

      console.log(`  ${post.slug} (${len} chars, ${len - MAX_LENGTH} over)`);
      console.log(`  Current:   ${visible}|${truncated}`);
      console.log(`  Suggested: ${visible}...`);
      console.log('');
    }
  }

  if (!longOnly && tooShort.length > 0) {
    console.log(`=== Too Short (<${MIN_LENGTH} chars) — ${tooShort.length} posts ===\n`);
    console.log('Short descriptions waste SERP real estate. Add more detail.\n');

    for (const post of tooShort) {
      const len = (post.description || '').length;
      console.log(`  ${post.slug} (${len} chars, need ${MIN_LENGTH - len} more)`);
      console.log(`  Current: ${post.description || '(empty)'}`);
      console.log('');
    }
  }

  // Summary
  console.log('=== Summary ===\n');
  console.log(`  Total posts: ${postsToCheck.length}`);
  console.log(`  Optimal (${MIN_LENGTH}-${MAX_LENGTH} chars): ${justRight.length}`);
  console.log(`  Too long: ${tooLong.length}`);
  console.log(`  Too short: ${tooShort.length}`);

  if (tooLong.length > 0 || tooShort.length > 0) {
    console.log(`\n  ${tooLong.length + tooShort.length} descriptions need attention.\n`);
  } else {
    console.log('\n  All descriptions are within optimal range.\n');
  }
}

main().catch(err => {
  console.error('Description analysis failed:', err.message);
  process.exit(1);
});
