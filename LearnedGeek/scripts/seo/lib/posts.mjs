import { readFile, access } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
// __dirname = scripts/seo/lib/ — need 3 levels up to reach LearnedGeek/
const PROJECT_ROOT = join(__dirname, '..', '..', '..');
const CONTENT_ROOT = join(__dirname, '..', '..', '..', 'Content');
const WWWROOT = join(__dirname, '..', '..', '..', 'wwwroot');

export { PROJECT_ROOT, CONTENT_ROOT, WWWROOT };

export async function loadPosts({ includeScheduled = false } = {}) {
  const raw = await readFile(join(CONTENT_ROOT, 'posts.json'), 'utf-8');
  const { posts } = JSON.parse(raw);

  if (includeScheduled) return posts;

  const today = new Date();
  today.setHours(23, 59, 59, 999);
  return posts.filter(p => new Date(p.date) <= today);
}

export function getMarkdownPath(slug) {
  return join(CONTENT_ROOT, 'posts', `${slug}.md`);
}

export function getImagePath(imagePath) {
  return join(WWWROOT, imagePath.replace(/^\//, '').replace(/\//g, '/'));
}

export async function fileExists(filePath) {
  try {
    await access(filePath);
    return true;
  } catch {
    return false;
  }
}

export const VALID_CATEGORIES = ['tech', 'writing', 'gaming', 'project', 'personal', 'opinion'];

export const STATIC_SITEMAP_URLS = [
  '/',
  '/Home/About',
  '/Home/Work',
  '/Home/Services',
  '/Home/Contact',
  '/Home/Writing',
  '/Home/Privacy',
  '/Home/SmsPrivacy',
  '/Home/SmsTerms',
  '/Home/SmsAssistant',
  '/Home/RemoteWorkPolicy',
  '/Blog'
];

export const BASE_URL = 'https://learnedgeek.com';
