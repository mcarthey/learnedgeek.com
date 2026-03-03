#!/usr/bin/env node

/**
 * reschedule.mjs — Reorder and redate scheduled blog posts
 *
 * Reads a desired slug order from a JSON file, assigns dates at a
 * specified cadence starting from a given date, and updates posts.json.
 *
 * Usage:
 *   node scripts/seo/reschedule.mjs --plan plan.json --dry-run
 *   node scripts/seo/reschedule.mjs --plan plan.json
 *
 * Plan file format:
 *   { "startDate": "2026-03-06", "intervalDays": 3, "order": ["slug1", "slug2", ...] }
 */

import { readFile, writeFile } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const POSTS_JSON = join(__dirname, '..', '..', 'Content', 'posts.json');

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const planIdx = args.indexOf('--plan');

if (planIdx === -1 || !args[planIdx + 1]) {
  console.error('Usage: node reschedule.mjs --plan <plan.json> [--dry-run]');
  process.exit(1);
}

const planPath = args[planIdx + 1].startsWith('/')
  ? args[planIdx + 1]
  : join(process.cwd(), args[planIdx + 1]);

async function main() {
  const raw = await readFile(POSTS_JSON, 'utf-8');
  const data = JSON.parse(raw);

  const plan = JSON.parse(await readFile(planPath, 'utf-8'));
  const { startDate, intervalDays, order } = plan;

  if (!startDate || !intervalDays || !Array.isArray(order)) {
    console.error('Plan must have startDate, intervalDays, and order array.');
    process.exit(1);
  }

  const postsBySlug = new Map(data.posts.map(p => [p.slug, p]));

  // Validate all slugs exist
  const missing = order.filter(slug => !postsBySlug.has(slug));
  if (missing.length > 0) {
    console.error('Unknown slugs in plan:', missing.join(', '));
    process.exit(1);
  }

  // Check for slugs in posts.json not in plan
  const today = new Date();
  today.setHours(23, 59, 59, 999);
  const scheduledSlugs = new Set(
    data.posts.filter(p => new Date(p.date) > today).map(p => p.slug)
  );
  const unplanned = [...scheduledSlugs].filter(s => !order.includes(s));
  if (unplanned.length > 0) {
    console.warn(`WARN: ${unplanned.length} scheduled post(s) not in plan (will keep existing dates):`);
    unplanned.forEach(s => console.warn(`  - ${s}`));
    console.warn('');
  }

  // Assign new dates
  console.log(`\nReschedule Preview — ${dryRun ? 'DRY RUN' : 'APPLYING'}\n`);
  console.log(`  Start: ${startDate}  |  Interval: every ${intervalDays} days  |  Posts: ${order.length}\n`);

  const current = new Date(startDate + 'T12:00:00');
  const changes = [];

  for (const slug of order) {
    const post = postsBySlug.get(slug);
    const newDate = current.toISOString().split('T')[0];
    const oldDate = post.date.split('T')[0];
    const changed = newDate !== oldDate;

    changes.push({ slug, oldDate, newDate, changed });
    console.log(`  ${changed ? '~' : ' '} ${newDate}  ${slug}${changed ? `  (was ${oldDate})` : ''}`);

    if (!dryRun) {
      post.date = current.toISOString().replace('.000Z', '');
    }

    current.setDate(current.getDate() + intervalDays);
  }

  const changedCount = changes.filter(c => c.changed).length;
  const lastDate = changes[changes.length - 1]?.newDate;
  console.log(`\n  ${changedCount} date(s) changed. Queue ends: ${lastDate}\n`);

  if (!dryRun) {
    await writeFile(POSTS_JSON, JSON.stringify(data, null, 2) + '\n', 'utf-8');
    console.log(`  ✓ posts.json updated.\n`);
  } else {
    console.log('  (Dry run — no changes written. Remove --dry-run to apply.)\n');
  }
}

main().catch(err => {
  console.error('Reschedule failed:', err.message);
  process.exit(1);
});
