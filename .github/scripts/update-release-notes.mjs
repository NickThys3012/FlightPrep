// Reads AI description from /tmp/ai_description.txt, bumps the version,
// and prepends a new entry to src/FlightPrep/wwwroot/release-notes.json.

const {readFileSync, writeFileSync, existsSync} = await import('fs');

const jsonPath = 'src/FlightPrep/wwwroot/release-notes.json';
const data = JSON.parse(
    existsSync(jsonPath) ? readFileSync(jsonPath, 'utf8') : '{"currentVersion":"0.0.0","entries":[]}'
);

const labels = JSON.parse(process.env.PR_LABELS || '[]');
const title = process.env.PR_TITLE || '';
const labelStr = labels.join(' ').toLowerCase();
const aiDesc = readFileSync('/tmp/ai_description.txt', 'utf8').trim();

// Version bump rules:
//   [feature] / label feature   → major  (X+1.0.0)
//   [refactor] / label refactor  → minor  (X.X+1.0)
//   [BUG] / [fix] / label bug    → patch  (X.X.X+1)
let bumpType = 'patch';
if (/\[feature\]/i.test(title) || labelStr.includes('feature') || labelStr.includes('enhancement'))
    bumpType = 'major';
else if (/\[refactor\]/i.test(title) || labelStr.includes('refactor'))
    bumpType = 'minor';
else if (/\[bug\]/i.test(title) || /\[fix\]/i.test(title) || labelStr.includes('bug') || labelStr.includes('fix'))
    bumpType = 'patch';

const [maj, min, pat] = (data.currentVersion || '0.0.0').split('.').map(Number);
const newVersion =
    bumpType === 'major' ? `${maj + 1}.0.0` :
        bumpType === 'minor' ? `${maj}.${min + 1}.0` :
            `${maj}.${min}.${pat + 1}`;

const entry = {
    pr: parseInt(process.env.PR_NUMBER),
    version: newVersion,
    title,
    description: aiDesc,
    author: process.env.PR_AUTHOR || '',
    labels,
    date: process.env.MERGE_DATE || new Date().toISOString(),
};

data.currentVersion = newVersion;
data.entries = [entry, ...(data.entries || [])];
writeFileSync(jsonPath, JSON.stringify(data, null, 2) + '\n');
console.log(`v${newVersion} (${bumpType}) — PR #${entry.pr}: ${title}`);
