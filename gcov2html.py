import os
import sys
import glob
import html
import json
import shutil
import webbrowser
from pathlib import Path
from datetime import datetime

# ========================
# Cấu hình
# ========================
OUTPUT_DIR = "coverage_html"
INDEX_FILE = os.path.join(OUTPUT_DIR, "index.html")
HISTORY_FILE = os.path.join(OUTPUT_DIR, "coverage_history.json")

LAZY_LOAD_THRESHOLD = 1000
CHUNK_SIZE = 100

# ========================
# Dọn dẹp thư mục HTML cũ
# ========================
if os.path.exists(OUTPUT_DIR):
    shutil.rmtree(OUTPUT_DIR)
os.makedirs(OUTPUT_DIR, exist_ok=True)

# ========================
# Lịch sử coverage
# ========================
def load_history():
    if os.path.exists(HISTORY_FILE):
        try:
            with open(HISTORY_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except:
            return []
    return []

def save_history(entry):
    history = load_history()
    history.append(entry)
    if len(history) > 30:
        history = history[-30:]
    with open(HISTORY_FILE, 'w', encoding='utf-8') as f:
        json.dump(history, f, indent=2, ensure_ascii=False)

# ========================
# Chuyển .gcov → HTML (giao diện chuyên nghiệp)
# ========================
def gcov_to_html(gcov_file, html_file, relative_path=""):
    try:
        with open(gcov_file, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
    except Exception as e:
        print(f"[ERROR] Không đọc được file {gcov_file}: {e}")
        return 0, 0, 0.0

    total_instrumented = 0
    covered = 0
    uncovered_indices = []
    branch_total = 0
    branch_taken = 0
    branch_percent = 0.0

    for line in lines:
        if "blocks executed" in line:
            try:
                percent_str = line.split("blocks executed ")[-1].replace('%', '').strip()
                branch_percent = float(percent_str)
            except:
                branch_percent = 0.0
            break

    parsed_lines = []
    i = 0
    while i < len(lines):
        line = lines[i]
        parts = line.split(':', 2)
        if len(parts) < 3:
            i += 1
            continue

        count_str = parts[0].strip()
        line_num_str = parts[1].strip()
        code = parts[2].rstrip('\n')

        is_instrumented = count_str != '-' and not count_str.startswith('====')
        is_covered = is_instrumented and count_str.isdigit() and int(count_str) > 0
        is_uncovered = '#####' in count_str

        if is_instrumented:
            total_instrumented += 1
            if is_covered:
                covered += 1
            elif is_uncovered:
                uncovered_indices.append(i)

        code = html.escape(code)
        line_num_str = html.escape(line_num_str)
        count_str_display = html.escape(count_str).ljust(8)

        if count_str == '-':
            css_class = 'uninstrumented'
        elif is_uncovered:
            css_class = 'uncovered'
        else:
            css_class = 'covered'

        prefix = ""
        if is_uncovered:
            prefix = "[MISS] "
        elif is_covered:
            prefix = f"[{count_str_display.strip()}x] "

        html_line = f"<span class='{css_class}' data-line='{i}'><span class='line-num'>{line_num_str}</span> {prefix}{code}</span>"

        parsed_lines.append({
            'html': html_line,
            'class': css_class,
            'index': i
        })

        if code.strip().endswith('{') or 'if (' in code or 'else' in code or 'while' in code or 'for' in code:
            j = i + 1
            while j < len(lines) and lines[j].strip().startswith('branch'):
                branch_line = lines[j].strip()
                if 'taken' in branch_line:
                    branch_total += 1
                    if 'taken 0' not in branch_line:
                        branch_taken += 1
                j += 1

        i += 1

    coverage_percent = (covered / total_instrumented * 100) if total_instrumented > 0 else 0.0
    if branch_total > 0:
        branch_percent = (branch_taken / branch_total * 100)

    display_file_name = os.path.basename(gcov_file)
    if display_file_name.endswith('.gcov'):
        display_file_name = display_file_name[:-5]

    # Tạo breadcrumb
    breadcrumb_parts = ["<a href='index.html'>Home</a>"]
    if relative_path:
        folders = relative_path.split(os.sep)
        path_so_far = ""
        for folder in folders:
            if not folder:
                continue
            path_so_far = os.path.join(path_so_far, folder) if path_so_far else folder
            breadcrumb_parts.append(f"<a href='#'>{html.escape(folder)}</a>")
    breadcrumb_parts.append(html.escape(display_file_name))
    breadcrumb = " > ".join(breadcrumb_parts)

    use_lazy_load = len(parsed_lines) > LAZY_LOAD_THRESHOLD

    # 🎨 GIAO DIỆN CHUYÊN NGHIỆP - CSS HIỆN ĐẠI
    html_content = f'''
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Coverage: {html.escape(display_file_name)}</title>
    <style>
        :root {{
            --primary: #4361ee;
            --success: #06d6a0;
            --danger: #ef476f;
            --warning: #ffd166;
            --dark: #2b2d42;
            --light: #f8f9fa;
            --gray: #adb5bd;
            --border: #e9ecef;
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: var(--dark);
            background: #fafafa;
            padding: 20px;
        }}

        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.05);
            overflow: hidden;
        }}

        header {{
            background: linear-gradient(135deg, var(--primary), #3a0ca3);
            color: white;
            padding: 30px 40px;
            position: relative;
        }}

        .actions {{
            position: absolute;
            top: 20px;
            right: 20px;
            display: flex;
            gap: 10px;
        }}

        .btn {{
            padding: 8px 16px;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 500;
            transition: all 0.2s;
        }}

        .btn-dark-mode {{
            background: rgba(255,255,255,0.2);
            color: white;
        }}

        .btn-dark-mode:hover {{
            background: rgba(255,255,255,0.3);
        }}

        .btn-next {{
            background: var(--warning);
            color: var(--dark);
        }}

        .btn-next:hover {{
            background: #ffc44d;
        }}

        h1 {{
            font-size: 2rem;
            margin-bottom: 10px;
            font-weight: 700;
        }}

        .breadcrumb {{
            font-size: 0.9rem;
            opacity: 0.9;
            margin-bottom: 15px;
        }}

        .stats {{
            display: flex;
            gap: 30px;
            margin: 20px 0;
            flex-wrap: wrap;
        }}

        .stat-card {{
            flex: 1;
            min-width: 200px;
            background: rgba(255,255,255,0.15);
            padding: 20px;
            border-radius: 10px;
            backdrop-filter: blur(10px);
        }}

        .stat-title {{
            font-size: 0.9rem;
            opacity: 0.9;
            margin-bottom: 5px;
        }}

        .stat-value {{
            font-size: 1.8rem;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 10px;
        }}

        .badge {{
            padding: 4px 12px;
            border-radius: 20px;
            font-weight: 600;
            font-size: 0.9rem;
        }}

        .badge-success {{
            background: var(--success);
            color: white;
        }}

        .badge-warning {{
            background: var(--warning);
            color: var(--dark);
        }}

        .badge-danger {{
            background: var(--danger);
            color: white;
        }}

        .back-link {{
            display: inline-block;
            margin: 20px 0;
            color: var(--primary);
            text-decoration: none;
            font-weight: 500;
            padding: 10px 20px;
            border: 2px solid var(--primary);
            border-radius: 6px;
            transition: all 0.2s;
        }}

        .back-link:hover {{
            background: var(--primary);
            color: white;
        }}

        pre {{
            background: #2d2d2d;
            color: #f8f8f2;
            padding: 30px;
            font-family: 'Fira Code', 'Consolas', monospace;
            font-size: 14px;
            line-height: 1.5;
            overflow-x: auto;
            border-radius: 0 0 12px 12px;
        }}

        .covered {{ color: #a6e22e; }}
        .uncovered {{ color: #f92672; background: rgba(249, 38, 114, 0.1); }}
        .uninstrumented {{ color: #666; }}

        .line-num {{
            color: #666;
            margin-right: 15px;
            user-select: none;
        }}

        #coverage-container {{
            background: #2d2d2d;
            color: #f8f8f2;
            padding: 30px;
            font-family: 'Fira Code', 'Consolas', monospace;
            font-size: 14px;
            line-height: 1.5;
            border-radius: 0 0 12px 12px;
        }}

        #loader {{
            text-align: center;
            padding: 20px;
            color: var(--gray);
            font-size: 0.9rem;
        }}

        /* Dark Mode */
        body.dark-mode {{
            background: #1a1a1a;
            color: #e0e0e0;
        }}

        body.dark-mode .container {{
            background: #252525;
            box-shadow: 0 5px 15px rgba(0,0,0,0.2);
        }}

        body.dark-mode pre {{
            background: #1e1e1e;
        }}

        body.dark-mode .uncovered {{
            background: rgba(249, 38, 114, 0.2);
        }}

        @media (max-width: 768px) {{
            .stats {{
                flex-direction: column;
            }}
            header {{
                padding: 20px;
            }}
            h1 {{
                font-size: 1.5rem;
            }}
        }}
    </style>
</head>
<body>
    <div class="container">
        <header>
            <div class="actions">
                <button class="btn btn-dark-mode" id="themeToggle">🌙 Dark Mode</button>
                <button class="btn btn-next" id="nextUncovered">⏭️ Next Uncovered</button>
            </div>
            <div class="breadcrumb">{breadcrumb}</div>
            <h1>{html.escape(display_file_name)}</h1>
            <div class="stats">
                <div class="stat-card">
                    <div class="stat-title">C0 Coverage (Statement)</div>
                    <div class="stat-value">
                        {coverage_percent:.1f}%
                        <span class="badge {'badge-success' if coverage_percent >= 80 else 'badge-warning' if coverage_percent >= 50 else 'badge-danger'}">
                            {covered}/{total_instrumented}
                        </span>
                    </div>
                </div>
                <div class="stat-card">
                    <div class="stat-title">C1 Coverage (Branch)</div>
                    <div class="stat-value">
                        {branch_percent:.1f}%
                        <span class="badge {'badge-success' if branch_percent >= 80 else 'badge-warning' if branch_percent >= 50 else 'badge-danger'}">
                            {branch_taken}/{branch_total}
                        </span>
                    </div>
                </div>
            </div>
        </header>

        <a href="index.html" class="back-link">⬅️ Back to Summary</a>

'''

    if use_lazy_load:
        html_content += '''
        <div id="coverage-container"></div>
        <div id="loader">Loading more lines... ▼</div>

        <script>
            const allLines = [
'''
        for item in parsed_lines:
            html_content += f'                `{item["html"]}`,\n'

        html_content = html_content.rstrip(',\n') + '''
            ];

            const container = document.getElementById('coverage-container');
            const loader = document.getElementById('loader');
            let loadedCount = 0;
            const chunkSize = ''' + str(CHUNK_SIZE) + ''';
            let uncoveredSpans = [];
            let currentIndex = -1;

            function loadChunk() {
                const fragment = document.createDocumentFragment();
                const end = Math.min(loadedCount + chunkSize, allLines.length);
                for (let i = loadedCount; i < end; i++) {
                    const div = document.createElement('div');
                    div.innerHTML = allLines[i];
                    const span = div.firstChild;
                    if (span.classList.contains('uncovered')) {
                        uncoveredSpans.push(span);
                    }
                    fragment.appendChild(span);
                }
                container.appendChild(fragment);
                loadedCount = end;

                if (loadedCount >= allLines.length) {
                    loader.style.display = 'none';
                }
            }

            loadChunk();

            container.addEventListener('scroll', () => {
                if (container.scrollTop + container.clientHeight >= container.scrollHeight - 200) {
                    if (loadedCount < allLines.length) {
                        loadChunk();
                    }
                }
            });

            document.getElementById('nextUncovered').addEventListener('click', () => {
                if (uncoveredSpans.length === 0) return;
                currentIndex = (currentIndex + 1) % uncoveredSpans.length;
                const target = uncoveredSpans[currentIndex];
                uncoveredSpans.forEach(el => el.classList.remove('highlighted'));
                target.classList.add('highlighted');
                target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            });

            setTimeout(() => {
                const firstUncovered = document.querySelector('.uncovered');
                if (firstUncovered) {
                    firstUncovered.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    firstUncovered.classList.add('highlighted');
                    currentIndex = uncoveredSpans.indexOf(firstUncovered);
                }
            }, 100);

            const toggle = document.getElementById('themeToggle');
            toggle.addEventListener('click', () => {
                document.body.classList.toggle('dark-mode');
                toggle.textContent = document.body.classList.contains('dark-mode') ? '☀️ Light Mode' : '🌙 Dark Mode';
            });
        </script>
'''

    else:
        html_content += '''
        <pre>
'''
        for item in parsed_lines:
            html_content += item['html'] + '\n'
        html_content += '''
        </pre>

        <script>
            const uncoveredSpans = Array.from(document.querySelectorAll('.uncovered'));
            let currentIndex = -1;

            document.getElementById('nextUncovered').addEventListener('click', () => {
                if (uncoveredSpans.length === 0) return;
                currentIndex = (currentIndex + 1) % uncoveredSpans.length;
                const target = uncoveredSpans[currentIndex];
                uncoveredSpans.forEach(el => el.classList.remove('highlighted'));
                target.classList.add('highlighted');
                target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            });

            window.addEventListener('load', () => {
                const firstUncovered = document.querySelector('.uncovered');
                if (firstUncovered) {
                    firstUncovered.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    firstUncovered.classList.add('highlighted');
                    currentIndex = 0;
                }
            });

            const toggle = document.getElementById('themeToggle');
            toggle.addEventListener('click', () => {
                document.body.classList.toggle('dark-mode');
                toggle.textContent = document.body.classList.contains('dark-mode') ? '☀️ Light Mode' : '🌙 Dark Mode';
            });
        </script>
'''

    html_content += '''
    </div>
</body>
</html>
'''

    try:
        with open(html_file, 'w', encoding='utf-8') as f:
            f.write(html_content)
        status = " (lazy-load)" if use_lazy_load else ""
        print(f"[OK] {gcov_file} → {os.path.basename(html_file)} | C0: {coverage_percent:.1f}% | C1: {branch_percent:.1f}%{status}")
    except Exception as e:
        print(f"[ERROR] Ghi file HTML thất bại: {e}")

    return covered, total_instrumented, branch_percent

# ========================
# Tạo cấu trúc cây thư mục
# ========================
def build_tree(reports):
    tree = {}
    for report in reports:
        if not isinstance(report, dict):
            continue
        if 'name' not in report or 'html_file' not in report:
            continue

        relative_path = report.get('relative_path', '')
        if not isinstance(relative_path, str):
            relative_path = ''

        if relative_path:
            path_parts = relative_path.split(os.sep)
        else:
            path_parts = [report['name']]

        current_level = tree

        for part in path_parts[:-1]:
            if not isinstance(part, str) or not part:
                continue
            if part not in current_level:
                current_level[part] = {}
            elif not isinstance(current_level[part], dict):
                current_level[part] = {}
            current_level = current_level[part]

        file_name = path_parts[-1]
        if not isinstance(file_name, str) or not file_name:
            file_name = report['name']

        original_file_name = file_name
        counter = 1
        while file_name in current_level and isinstance(current_level[file_name], dict):
            file_name = f"{original_file_name}_file{counter}"
            counter += 1

        current_level[file_name] = report

    return tree

def render_tree_to_html(tree, level=0):
    html_lines = []
    keys = sorted(tree.keys(), key=lambda x: (isinstance(tree[x], dict) and 'covered' not in tree[x], str(x).lower()))
    for key in keys:
        item = tree[key]
        indent = "  " * level
        if isinstance(item, dict) and 'covered' in item and 'total' in item:
            report = item
            c0_percent = (report['covered'] / report['total'] * 100) if report['total'] > 0 else 0
            c1_percent = report.get('branch_percent', 0)
            display_name = report.get('name', key)
            c0_badge = "badge-success" if c0_percent >= 80 else "badge-warning" if c0_percent >= 50 else "badge-danger"
            c1_badge = "badge-success" if c1_percent >= 80 else "badge-warning" if c1_percent >= 50 else "badge-danger"
            html_lines.append(
                f'{indent}<div style="margin: 10px 0; padding: 15px; border-radius: 8px; background: #f8f9fa; border-left: 4px solid #4361ee;">'
                f'📄 <strong><a href="{html.escape(report["html_file"])}" style="color: #4361ee; text-decoration: none;">{html.escape(display_name)}</a></strong><br/>'
                f'<span style="display: inline-block; margin: 5px 10px 0 0; padding: 3px 10px; border-radius: 12px; background: #06d6a0; color: white; font-size: 0.85rem;">C0: {c0_percent:.0f}%</span>'
                f'<span style="display: inline-block; padding: 3px 10px; border-radius: 12px; background: #ffd166; color: #2b2d42; font-size: 0.85rem;">C1: {c1_percent:.0f}%</span>'
                f'</div>'
            )
        elif isinstance(item, dict):
            html_lines.append(f'{indent}<details style="margin: 10px 0;">')
            html_lines.append(f'{indent}<summary style="padding: 10px 15px; background: #e9ecef; border-radius: 8px; cursor: pointer; font-weight: 600;">📁 {html.escape(str(key))}</summary>')
            html_lines.append(f'{indent}<div style="margin-left: 20px; padding: 10px; border-left: 2px solid #dee2e6;">')
            html_lines.extend(render_tree_to_html(item, level + 1))
            html_lines.append(f'{indent}</div>')
            html_lines.append(f'{indent}</details>')
        else:
            html_lines.append(f'{indent}<div style="color: #ef476f; padding: 10px;">⚠️ {html.escape(str(key))}</div>')

    return html_lines

# ========================
# Tạo trang index.html (giao diện chuyên nghiệp)
# ========================
def generate_index_html(reports):
    total_covered = sum(r['covered'] for r in reports)
    total_instrumented = sum(r['total'] for r in reports)
    total_branch_taken = sum(r.get('branch_taken', 0) for r in reports)
    total_branch_total = sum(r.get('branch_total', 0) for r in reports)

    overall_c0 = (total_covered / total_instrumented * 100) if total_instrumented > 0 else 0
    overall_c1 = (total_branch_taken / total_branch_total * 100) if total_branch_total > 0 else 0

    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    save_history({
        "timestamp": now,
        "total_covered": total_covered,
        "total_instrumented": total_instrumented,
        "overall_c0": overall_c0,
        "overall_c1": overall_c1,
        "file_count": len(reports)
    })

    history = load_history()
    if len(history) > 1:
        last_c0 = history[-2]['overall_c0']
        last_c1 = history[-2]['overall_c1']
        delta_c0 = overall_c0 - last_c0
        delta_c1 = overall_c1 - last_c1
        trend_c0 = f" <span style='color: {'green' if delta_c0 > 0 else 'red'};'>{'▲' if delta_c0 > 0 else '▼'}{abs(delta_c0):.1f}%</span>" if delta_c0 != 0 else ""
        trend_c1 = f" <span style='color: {'green' if delta_c1 > 0 else 'red'};'>{'▲' if delta_c1 > 0 else '▼'}{abs(delta_c1):.1f}%</span>" if delta_c1 != 0 else ""
    else:
        trend_c0 = trend_c1 = ""

    tree = build_tree(reports)
    tree_html_lines = render_tree_to_html(tree)
    tree_html = "\n".join(tree_html_lines)

    html_content = f'''
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>📊 Code Coverage Report</title>
    <style>
        :root {{
            --primary: #4361ee;
            --success: #06d6a0;
            --danger: #ef476f;
            --warning: #ffd166;
            --dark: #2b2d42;
            --light: #f8f9fa;
            --gray: #adb5bd;
            --border: #e9ecef;
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: var(--dark);
            background: #f5f7fa;
            padding: 20px;
        }}

        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 16px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.08);
            overflow: hidden;
        }}

        header {{
            background: linear-gradient(135deg, #4361ee, #3a0ca3);
            color: white;
            padding: 40px;
            text-align: center;
        }}

        h1 {{
            font-size: 2.5rem;
            font-weight: 800;
            margin-bottom: 10px;
            letter-spacing: -0.5px;
        }}

        .subtitle {{
            font-size: 1.1rem;
            opacity: 0.9;
            margin-bottom: 30px;
        }}

        .controls {{
            padding: 30px 40px;
            background: #f8f9fa;
            border-bottom: 1px solid var(--border);
        }}

        #searchInput {{
            width: 100%;
            max-width: 500px;
            padding: 12px 20px;
            border: 2px solid var(--border);
            border-radius: 50px;
            font-size: 1rem;
            outline: none;
            transition: all 0.3s;
        }}

        #searchInput:focus {{
            border-color: var(--primary);
            box-shadow: 0 0 0 3px rgba(67, 97, 238, 0.1);
        }}

        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 30px;
            padding: 40px;
        }}

        .stat-card {{
            background: white;
            border-radius: 16px;
            padding: 30px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.05);
            border: 1px solid var(--border);
            text-align: center;
            transition: transform 0.2s;
        }}

        .stat-card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 8px 25px rgba(0,0,0,0.1);
        }}

        .stat-title {{
            font-size: 1.1rem;
            color: var(--gray);
            margin-bottom: 15px;
            font-weight: 500;
        }}

        .stat-value {{
            font-size: 3rem;
            font-weight: 800;
            background: linear-gradient(135deg, var(--primary), #3a0ca3);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin: 10px 0;
        }}

        .stat-subtitle {{
            font-size: 0.9rem;
            color: var(--gray);
        }}

        .section-title {{
            padding: 0 40px 20px;
            font-size: 1.5rem;
            font-weight: 700;
            color: var(--dark);
            border-bottom: 2px solid var(--border);
            margin: 40px 0 20px;
        }}

        #fileTree {{
            padding: 0 40px 40px;
        }}

        .btn-dark-mode {{
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 12px 24px;
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            border-radius: 50px;
            cursor: pointer;
            font-weight: 600;
            backdrop-filter: blur(10px);
            transition: all 0.3s;
        }}

        .btn-dark-mode:hover {{
            background: rgba(255,255,255,0.3);
        }}

        body.dark-mode {{
            background: #121212;
            color: #e0e0e0;
        }}

        body.dark-mode .container {{
            background: #1e1e1e;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }}

        body.dark-mode .stat-card {{
            background: #2d2d2d;
            border-color: #3a3a3a;
        }}

        body.dark-mode .controls {{
            background: #252525;
            border-color: #3a3a3a;
        }}

        @media (max-width: 768px) {{
            .stats-grid {{
                grid-template-columns: 1fr;
            }}
            header {{
                padding: 30px 20px;
            }}
            h1 {{
                font-size: 2rem;
            }}
            .stats-grid {{
                padding: 20px;
            }}
        }}
    </style>
</head>
<body>
    <button class="btn-dark-mode" id="themeToggle">🌙 Dark Mode</button>
    <div class="container">
        <header>
            <h1>📊 Code Coverage Report</h1>
            <p class="subtitle">Comprehensive C0 & C1 Coverage Analysis</p>
        </header>

        <div class="controls">
            <input type="text" id="searchInput" placeholder="🔍 Search files by name...">
        </div>

        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-title">Overall C0 Coverage</div>
                <div class="stat-value">{overall_c0:.1f}%</div>
                <div class="stat-subtitle">{total_covered:,} / {total_instrumented:,} lines {trend_c0}</div>
            </div>
            <div class="stat-card">
                <div class="stat-title">Overall C1 Coverage</div>
                <div class="stat-value">{overall_c1:.1f}%</div>
                <div class="stat-subtitle">{total_branch_taken:,} / {total_branch_total:,} branches {trend_c1}</div>
            </div>
            <div class="stat-card">
                <div class="stat-title">Total Files</div>
                <div class="stat-value">{len(reports)}</div>
                <div class="stat-subtitle">Analyzed on {now}</div>
            </div>
        </div>

        <h2 class="section-title">📁 Project Structure</h2>
        <div id="fileTree">
{tree_html}
        </div>
    </div>

    <script>
        const toggle = document.getElementById('themeToggle');
        toggle.addEventListener('click', () => {{
            document.body.classList.toggle('dark-mode');
            toggle.textContent = document.body.classList.contains('dark-mode') ? '☀️ Light Mode' : '🌙 Dark Mode';
        }});

        document.getElementById('searchInput').addEventListener('input', function(e) {{
            const term = e.target.value.toLowerCase();
            const items = document.querySelectorAll('#fileTree div, #fileTree details');
            items.forEach(item => {{
                const text = item.textContent.toLowerCase();
                item.style.display = text.includes(term) ? '' : 'none';
            }});
        }});
    </script>
</body>
</html>
'''

    with open(INDEX_FILE, 'w', encoding='utf-8') as f:
        f.write(html_content)

    print(f"\n✅ [TỔNG KẾT] C0: {overall_c0:.1f}% | C1: {overall_c1:.1f}%")
    print(f"📁 Mở file: {os.path.abspath(INDEX_FILE)} để xem báo cáo!")

# ========================
# Main
# ========================
def main():
    gcov_files = glob.glob("**/*.gcov", recursive=True)
    if not gcov_files:
        print("[!] Không tìm thấy file .gcov nào.")
        print("→ Hãy chạy `gcov -b your_file.c` để sinh file .gcov")
        sys.exit(1)

    reports = []

    for gcov_file in gcov_files:
        relative_dir = os.path.dirname(gcov_file)
        html_filename = gcov_file.replace('.gcov', '.html').replace(os.sep, '_')
        html_file = os.path.join(OUTPUT_DIR, html_filename)

        covered, total, branch_percent = gcov_to_html(gcov_file, html_file, relative_dir)
        if total > 0:
            base_name = os.path.basename(gcov_file)
            if base_name.endswith('.gcov'):
                display_name = base_name[:-5]
            else:
                display_name = base_name

            reports.append({
                'name': display_name,
                'covered': covered,
                'total': total,
                'branch_percent': branch_percent,
                'html_file': html_filename,
                'relative_path': gcov_file.replace('.gcov', '')
            })

    if reports:
        generate_index_html(reports)
        # try:
        #     webbrowser.open('file://' + os.path.abspath(INDEX_FILE))
        # except:
        #     print(f"🌐 Mở thủ công: {os.path.abspath(INDEX_FILE)}")
    else:
        print("[!] Không có dữ liệu coverage hợp lệ.")

if __name__ == '__main__':
    main()