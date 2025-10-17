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
GCNO_DIR = "."

LAZY_LOAD_THRESHOLD = 1000  # Kích hoạt lazy load nếu số dòng > 1000
CHUNK_SIZE = 100            # Mỗi lần load thêm 100 dòng

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
# Chuyển .gcov → HTML (có lazy load nếu file lớn)
# ========================
def gcov_to_html(gcov_file, html_file, relative_path=""):
    try:
        with open(gcov_file, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
    except Exception as e:
        print(f"[ERROR] Không đọc được file {gcov_file}: {e}")
        return 0, 0

    total_instrumented = 0
    covered = 0
    uncovered_indices = []

    parsed_lines = []  # Lưu trữ dữ liệu từng dòng để dùng trong lazy load

    for i, line in enumerate(lines):
        parts = line.split(':', 2)
        if len(parts) < 3:
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

        # Escape HTML
        code = html.escape(code)
        line_num_str = html.escape(line_num_str)
        count_str_display = html.escape(count_str).ljust(8)

        # Xác định CSS class
        if count_str == '-':
            css_class = 'uninstrumented'
        elif is_uncovered:
            css_class = 'uncovered'
        else:
            css_class = 'covered'

        # Tạo HTML string cho dòng này
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

    coverage_percent = (covered / total_instrumented * 100) if total_instrumented > 0 else 0

    # Tạo breadcrumb
    breadcrumb_parts = ["<a href='index.html'>Home</a>"]
    if relative_path:
        folders = relative_path.split(os.sep)
        path_so_far = ""
        for folder in folders:
            path_so_far = os.path.join(path_so_far, folder) if path_so_far else folder
            breadcrumb_parts.append(f"<a href='#'>{html.escape(folder)}</a>")
    breadcrumb = " > ".join(breadcrumb_parts)

    # Xác định có dùng lazy load không
    use_lazy_load = len(parsed_lines) > LAZY_LOAD_THRESHOLD

    # Phần đầu HTML (CSS + JS)
    html_content = f'''
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Coverage: {html.escape(os.path.basename(gcov_file))}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background: #f9f9f9; }}
        .breadcrumb {{ margin-bottom: 20px; font-size: 14px; color: #555; }}
        pre {{ background: white; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1); line-height: 1.4; }}
        .covered {{ background-color: #d7f7d7; border-left: 4px solid #4caf50; padding-left: 10px; }}
        .uncovered {{ background-color: #f7d7d7; border-left: 4px solid #f44336; padding-left: 10px; }}
        .uninstrumented {{ background-color: #f0f0f0; color: #666; padding-left: 10px; }}
        .summary {{ background: #e8f4fd; padding: 10px; border-radius: 5px; margin-bottom: 20px; font-weight: bold; }}
        .line-num {{ color: #888; padding-right: 10px; }}
        #themeToggle, #nextUncovered {{
            position: fixed;
            top: 20px;
            padding: 8px 12px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            z-index: 100;
        }}
        #themeToggle {{ right: 20px; }}
        #nextUncovered {{ right: 150px; }}
        body.dark-mode {{ background: #1a1a1a; color: #eee; }}
        body.dark-mode pre {{ background: #2d2d2d; }}
        body.dark-mode .covered {{ background: #1a3a1a; }}
        body.dark-mode .uncovered {{ background: #3a1a1a; }}
        body.dark-mode .uninstrumented {{ background: #333; color: #aaa; }}
        .highlighted {{ outline: 2px solid gold !important; }}
        #loader {{ text-align: center; padding: 10px; color: #888; }}
    </style>
</head>
<body>
    <button id="themeToggle">🌙 Dark Mode</button>
    <button id="nextUncovered">⏭️ Next Uncovered Line</button>
    <div class="breadcrumb">{breadcrumb}</div>
    <h1>Coverage Report: {html.escape(os.path.basename(gcov_file))}</h1>
    <div class="summary">
        Coverage: {covered}/{total_instrumented} lines ({coverage_percent:.1f}%)
    </div>
    <p><a href="index.html">⬅️ Back to Summary</a></p>
'''

    if use_lazy_load:
        html_content += '''
    <pre id="coverage-container"></pre>
    <div id="loader">Loading more lines... ▼</div>

    <script>
        const allLines = [
'''
        # Đổ dữ liệu dòng vào mảng JS
        for item in parsed_lines:
            html_content += f'            `{item["html"]}`,\n'

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

        // Load chunk đầu tiên
        loadChunk();

        // Lazy load khi cuộn gần cuối
        container.addEventListener('scroll', () => {
            if (container.scrollTop + container.clientHeight >= container.scrollHeight - 200) {
                if (loadedCount < allLines.length) {
                    loadChunk();
                }
            }
        });

        // Next Uncovered
        document.getElementById('nextUncovered').addEventListener('click', () => {
            if (uncoveredSpans.length === 0) return;
            currentIndex = (currentIndex + 1) % uncoveredSpans.length;
            const target = uncoveredSpans[currentIndex];
            uncoveredSpans.forEach(el => el.classList.remove('highlighted'));
            target.classList.add('highlighted');
            target.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });

        // Auto scroll to first uncovered (sau khi load xong chunk đầu tiên)
        setTimeout(() => {
            const firstUncovered = document.querySelector('.uncovered');
            if (firstUncovered) {
                firstUncovered.scrollIntoView({ behavior: 'smooth', block: 'center' });
                firstUncovered.classList.add('highlighted');
                currentIndex = uncoveredSpans.indexOf(firstUncovered);
            }
        }, 100);

        // Dark Mode
        const toggle = document.getElementById('themeToggle');
        toggle.addEventListener('click', () => {
            document.body.classList.toggle('dark-mode');
            toggle.textContent = document.body.classList.contains('dark-mode') ? '☀️ Light Mode' : '🌙 Dark Mode';
        });
    </script>
'''

    else:
        # File nhỏ → render full như cũ
        html_content += '<pre>\n'
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
</body>
</html>
'''

    try:
        with open(html_file, 'w', encoding='utf-8') as f:
            f.write(html_content)
        status = " (lazy-load)" if use_lazy_load else ""
        print(f"[OK] {gcov_file} → {os.path.basename(html_file)} | {coverage_percent:.1f}%{status}")
    except Exception as e:
        print(f"[ERROR] Ghi file HTML thất bại: {e}")

    return covered, total_instrumented

# ========================
# Tạo cấu trúc cây thư mục
# ========================
def build_tree(reports):
    tree = {}
    for report in reports:
        path_parts = report['relative_path'].split(os.sep) if report['relative_path'] else [report['name']]
        current_level = tree
        for part in path_parts[:-1]:
            if part not in current_level:
                current_level[part] = {}
            current_level = current_level[part]
        # Make sure we don't overwrite a folder with a file
        if path_parts[-1] not in current_level or not isinstance(current_level[path_parts[-1]], dict):
            current_level[path_parts[-1]] = report
    return tree

def render_tree_to_html(tree, level=0):
    html_lines = []
    # Sort keys, put folders first, then files
    folder_keys = sorted([k for k in tree.keys() if isinstance(tree[k], dict)])
    file_keys = sorted([k for k in tree.keys() if not isinstance(tree[k], dict)])
    keys = folder_keys + file_keys
    
    for key in keys:
        item = tree[key]
        indent = "  " * level
        if isinstance(item, dict):
            html_lines.append(f'{indent}<details>')
            html_lines.append(f'{indent}<summary>📁 {html.escape(key)}</summary>')
            html_lines.append(f'{indent}<div style="margin-left: 20px;">')
            html_lines.extend(render_tree_to_html(item, level + 1))
            html_lines.append(f'{indent}</div>')
            html_lines.append(f'{indent}</details>')
        else:
            report = item
            percent = (report['covered'] / report['total'] * 100) if report['total'] > 0 else 0
            status_class = "high" if percent >= 80 else "medium" if percent >= 50 else "low"
            html_lines.append(
                f'{indent}<div>📄 <a href="{html.escape(report["html_file"])}">{html.escape(key)}</a> '
                f'<span class="{status_class}">({percent:.1f}%)</span></div>'
            )
    return html_lines

# ========================
# Tạo trang index.html
# ========================
def generate_index_html(reports):
    total_covered = sum(r['covered'] for r in reports)
    total_instrumented = sum(r['total'] for r in reports)
    overall_percent = (total_covered / total_instrumented * 100) if total_instrumented > 0 else 0

    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    save_history({
        "timestamp": now,
        "total_covered": total_covered,
        "total_instrumented": total_instrumented,
        "overall_percent": overall_percent,
        "file_count": len(reports)
    })

    history = load_history()
    if len(history) > 1:
        last_percent = history[-2]['overall_percent']
        delta = overall_percent - last_percent
        trend = f" ▲<span style='color:green'>+{delta:.1f}%</span>" if delta > 0 else \
                f" ▼<span style='color:red'>{delta:.1f}%</span>" if delta < 0 else " (không đổi)"
    else:
        trend = ""

    tree = build_tree(reports)
    tree_html_lines = render_tree_to_html(tree)
    tree_html = "\n".join(tree_html_lines)

    html_content = f'''
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>📊 Code Coverage Report - Tổng hợp</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }}
        h1 {{ color: #2c3e50; }}
        .controls {{ margin: 20px 0; }}
        #searchInput {{ padding: 8px; width: 300px; margin-right: 10px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; display: none; }}
        th, td {{ padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background-color: #4CAF50; color: white; }}
        tr:hover {{ background-color: #f5f5f5; }}
        .high {{ color: #4caf50; font-weight: bold; }}
        .medium {{ color: #ff9800; font-weight: bold; }}
        .low {{ color: #f44336; font-weight: bold; }}
        .summary {{ background: #e3f2fd; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        #themeToggle {{ position: fixed; top: 20px; right: 20px; padding: 8px 12px; border: none; border-radius: 4px; cursor: pointer; }}
        body.dark-mode {{ background: #1a1a1a; color: #eee; }}
        body.dark-mode .summary {{ background: #2a3a4a; }}
        body.dark-mode details {{ background: #2d2d2d; padding: 5px; border-radius: 4px; }}
    </style>
</head>
<body>
    <button id="themeToggle">🌙 Dark Mode</button>
    <h1>📊 Code Coverage Report</h1>
    <div class="summary">
        <h2>📈 Tổng quan</h2>
        <p><strong>Tổng số dòng đã đo:</strong> {total_instrumented}</p>
        <p><strong>Số dòng đã chạy:</strong> {total_covered}</p>
        <p><strong>Độ bao phủ tổng:</strong> <span class="{'high' if overall_percent >= 80 else 'medium' if overall_percent >= 50 else 'low'}">{overall_percent:.1f}%</span>{trend}</p>
        <p><strong>Lần cập nhật:</strong> {now}</p>
    </div>

    <div class="controls">
        <input type="text" id="searchInput" placeholder="🔍 Tìm file theo tên...">
    </div>

    <h2>📁 Cấu trúc dự án</h2>
    <div id="fileTree">
{tree_html}
    </div>

    <script>
        const toggle = document.getElementById('themeToggle');
        toggle.addEventListener('click', () => {{
            document.body.classList.toggle('dark-mode');
            toggle.textContent = document.body.classList.contains('dark-mode') ? '☀️ Light Mode' : '🌙 Dark Mode';
        }});

        document.getElementById('searchInput').addEventListener('input', function(e) {{
            const term = e.target.value.toLowerCase();
            const items = document.querySelectorAll('#fileTree div, #fileTree summary');
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

    print(f"\n✅ [TỔNG KẾT] Coverage tổng: {overall_percent:.1f}%")
    print(f"📁 Mở file: {os.path.abspath(INDEX_FILE)} để xem báo cáo!")

# ========================
# Main
# ========================
def main():
    gcov_files = glob.glob("**/*.gcov", recursive=True)
    if not gcov_files:
        print("[!] Không tìm thấy file .gcov nào.")
        print("→ Hãy chạy `gcov your_file.c` hoặc `gcov **/*.c` để sinh file .gcov")
        sys.exit(1)

    reports = []

    for gcov_file in gcov_files:
        relative_dir = os.path.dirname(gcov_file)
        html_filename = gcov_file.replace('.gcov', '.html').replace(os.sep, '_')
        html_file = os.path.join(OUTPUT_DIR, html_filename)

        covered, total = gcov_to_html(gcov_file, html_file, relative_dir)
        if total > 0:
            reports.append({
                'name': os.path.basename(gcov_file),
                'covered': covered,
                'total': total,
                'html_file': html_filename,
                'relative_path': gcov_file.replace('.gcov', '')
            })

    if reports:
        generate_index_html(reports)
        # Only try to open the browser if we're not in a headless environment
        try:
            webbrowser.open('file://' + os.path.abspath(INDEX_FILE))
        except:
            pass
    else:
        print("[!] Không có dữ liệu coverage hợp lệ.")

if __name__ == '__main__':
    main()