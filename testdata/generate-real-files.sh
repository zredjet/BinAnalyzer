#!/bin/bash
# generate-real-files.sh — macOS ネイティブツールで実ファイルを生成
# 使用方法: chmod +x testdata/generate-real-files.sh && testdata/generate-real-files.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$SCRIPT_DIR/real"
mkdir -p "$OUT_DIR"

generated=0
skipped=0
failed=0

log_ok()   { echo "  [OK]   $1"; generated=$((generated + 1)); }
log_skip() { echo "  [SKIP] $1 — $2"; skipped=$((skipped + 1)); }
log_fail() { echo "  [FAIL] $1 — $2"; failed=$((failed + 1)); }

echo "=== Real File Generator ==="
echo "Output: $OUT_DIR"
echo ""

# ---------- PNG (sips) ----------
if [ ! -f "$OUT_DIR/test.png" ]; then
    if command -v sips &>/dev/null; then
        # Create a 4x4 pixel TIFF first, then convert to PNG
        TMPBMP=$(mktemp /tmp/test_XXXX.bmp)
        # Create minimal BMP: 4x4, 24-bit
        python3 -c "
import struct, sys
width, height, bpp = 4, 4, 24
row_size = (width * 3 + 3) & ~3
pixel_data_size = row_size * height
file_size = 54 + pixel_data_size
# BMP Header
sys.stdout.buffer.write(b'BM')
sys.stdout.buffer.write(struct.pack('<I', file_size))
sys.stdout.buffer.write(struct.pack('<HH', 0, 0))
sys.stdout.buffer.write(struct.pack('<I', 54))
# DIB Header (BITMAPINFOHEADER)
sys.stdout.buffer.write(struct.pack('<I', 40))
sys.stdout.buffer.write(struct.pack('<i', width))
sys.stdout.buffer.write(struct.pack('<i', height))
sys.stdout.buffer.write(struct.pack('<HH', 1, bpp))
sys.stdout.buffer.write(struct.pack('<I', 0))  # compression
sys.stdout.buffer.write(struct.pack('<I', pixel_data_size))
sys.stdout.buffer.write(struct.pack('<i', 2835))
sys.stdout.buffer.write(struct.pack('<i', 2835))
sys.stdout.buffer.write(struct.pack('<I', 0))
sys.stdout.buffer.write(struct.pack('<I', 0))
# Pixel data (blue pixels)
for y in range(height):
    for x in range(width):
        sys.stdout.buffer.write(bytes([255, 0, 0]))  # BGR: blue
    sys.stdout.buffer.write(b'\x00' * (row_size - width * 3))
" > "$TMPBMP"
        sips -s format png "$TMPBMP" --out "$OUT_DIR/test.png" &>/dev/null
        rm -f "$TMPBMP"
        log_ok "PNG (sips)"
    else
        log_skip "PNG" "sips not found"
    fi
else
    log_skip "PNG" "already exists"
fi

# ---------- JPEG (sips) ----------
if [ ! -f "$OUT_DIR/test.jpg" ]; then
    if command -v sips &>/dev/null && [ -f "$OUT_DIR/test.png" ]; then
        sips -s format jpeg "$OUT_DIR/test.png" --out "$OUT_DIR/test.jpg" &>/dev/null
        log_ok "JPEG (sips)"
    else
        log_skip "JPEG" "sips not found or PNG not available"
    fi
else
    log_skip "JPEG" "already exists"
fi

# ---------- BMP (sips) ----------
if [ ! -f "$OUT_DIR/test.bmp" ]; then
    if command -v sips &>/dev/null && [ -f "$OUT_DIR/test.png" ]; then
        sips -s format bmp "$OUT_DIR/test.png" --out "$OUT_DIR/test.bmp" &>/dev/null
        log_ok "BMP (sips)"
    else
        log_skip "BMP" "sips not found or PNG not available"
    fi
else
    log_skip "BMP" "already exists"
fi

# ---------- TIFF (sips) ----------
if [ ! -f "$OUT_DIR/test.tiff" ]; then
    if command -v sips &>/dev/null && [ -f "$OUT_DIR/test.png" ]; then
        sips -s format tiff "$OUT_DIR/test.png" --out "$OUT_DIR/test.tiff" &>/dev/null
        log_ok "TIFF (sips)"
    else
        log_skip "TIFF" "sips not found or PNG not available"
    fi
else
    log_skip "TIFF" "already exists"
fi

# ---------- GZIP ----------
if [ ! -f "$OUT_DIR/test.gz" ]; then
    echo "Hello, GZIP!" | gzip > "$OUT_DIR/test.gz"
    log_ok "GZIP"
else
    log_skip "GZIP" "already exists"
fi

# ---------- TAR ----------
if [ ! -f "$OUT_DIR/test.tar" ]; then
    TMPTAR=$(mktemp -d /tmp/tar_test_XXXX)
    echo "Hello, TAR!" > "$TMPTAR/hello.txt"
    tar cf "$OUT_DIR/test.tar" -C "$TMPTAR" hello.txt
    rm -rf "$TMPTAR"
    log_ok "TAR"
else
    log_skip "TAR" "already exists"
fi

# ---------- ZIP ----------
if [ ! -f "$OUT_DIR/test.zip" ]; then
    TMPZIP=$(mktemp -d /tmp/zip_test_XXXX)
    echo "Hello, ZIP!" > "$TMPZIP/hello.txt"
    (cd "$TMPZIP" && zip -q "$OUT_DIR/test.zip" hello.txt)
    rm -rf "$TMPZIP"
    log_ok "ZIP"
else
    log_skip "ZIP" "already exists"
fi

# ---------- SQLite ----------
if [ ! -f "$OUT_DIR/test.sqlite" ]; then
    if command -v sqlite3 &>/dev/null; then
        sqlite3 "$OUT_DIR/test.sqlite" "CREATE TABLE test(id INTEGER PRIMARY KEY, name TEXT); INSERT INTO test VALUES(1, 'hello');"
        log_ok "SQLite (sqlite3)"
    else
        log_skip "SQLite" "sqlite3 not found"
    fi
else
    log_skip "SQLite" "already exists"
fi

# ---------- Mach-O (cc) ----------
if [ ! -f "$OUT_DIR/test.macho" ]; then
    if command -v cc &>/dev/null; then
        TMPC=$(mktemp /tmp/test_XXXX.c)
        echo 'int main(void) { return 0; }' > "$TMPC"
        cc -o "$OUT_DIR/test.macho" "$TMPC" 2>/dev/null && log_ok "Mach-O (cc)" || log_fail "Mach-O" "compilation failed"
        rm -f "$TMPC"
    else
        log_skip "Mach-O" "cc not found"
    fi
else
    log_skip "Mach-O" "already exists"
fi

# ---------- Java Class (javac) ----------
if [ ! -f "$OUT_DIR/test.class" ]; then
    if command -v javac &>/dev/null; then
        TMPJAVA=$(mktemp -d /tmp/java_test_XXXX)
        echo 'public class Test { public static void main(String[] args) {} }' > "$TMPJAVA/Test.java"
        javac "$TMPJAVA/Test.java" 2>/dev/null && cp "$TMPJAVA/Test.class" "$OUT_DIR/test.class" && log_ok "Java Class (javac)" || log_fail "Java Class" "compilation failed"
        rm -rf "$TMPJAVA"
    else
        log_skip "Java Class" "javac not found"
    fi
else
    log_skip "Java Class" "already exists"
fi

# ---------- LZ4 ----------
if [ ! -f "$OUT_DIR/test.lz4" ]; then
    if command -v lz4 &>/dev/null; then
        echo "Hello, LZ4!" | lz4 > "$OUT_DIR/test.lz4" 2>/dev/null
        log_ok "LZ4 (lz4)"
    else
        log_skip "LZ4" "lz4 not found (brew install lz4)"
    fi
else
    log_skip "LZ4" "already exists"
fi

# ---------- ICC (system copy) ----------
if [ ! -f "$OUT_DIR/test.icc" ]; then
    ICC_SRC="/System/Library/ColorSync/Profiles/sRGB Profile.icc"
    if [ -f "$ICC_SRC" ]; then
        cp "$ICC_SRC" "$OUT_DIR/test.icc"
        log_ok "ICC (system copy)"
    else
        # Try alternative path
        ICC_ALT=$(find /System/Library/ColorSync/Profiles/ -name "*.icc" -type f 2>/dev/null | head -1)
        if [ -n "$ICC_ALT" ]; then
            cp "$ICC_ALT" "$OUT_DIR/test.icc"
            log_ok "ICC (system copy: $(basename "$ICC_ALT"))"
        else
            log_skip "ICC" "no ICC profiles found in system"
        fi
    fi
else
    log_skip "ICC" "already exists"
fi

# ---------- OTF (system copy) ----------
if [ ! -f "$OUT_DIR/test.otf" ]; then
    OTF_SRC="/System/Library/Fonts/LastResort.otf"
    if [ -f "$OTF_SRC" ]; then
        cp "$OTF_SRC" "$OUT_DIR/test.otf"
        log_ok "OTF (system copy)"
    else
        # Try alternative
        OTF_ALT=$(find /System/Library/Fonts/ -name "*.otf" -type f 2>/dev/null | head -1)
        if [ -n "$OTF_ALT" ]; then
            cp "$OTF_ALT" "$OUT_DIR/test.otf"
            log_ok "OTF (system copy: $(basename "$OTF_ALT"))"
        else
            log_skip "OTF" "no OTF fonts found in system"
        fi
    fi
else
    log_skip "OTF" "already exists"
fi

# ---------- PDF (hand-crafted) ----------
if [ ! -f "$OUT_DIR/test.pdf" ]; then
    cat > "$OUT_DIR/test.pdf" << 'PDFEOF'
%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Hello PDF) Tj ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000266 00000 n
0000000360 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
441
%%EOF
PDFEOF
    log_ok "PDF (hand-crafted)"
else
    log_skip "PDF" "already exists"
fi

# ---------- PCAP (tcpdump or hand-crafted) ----------
if [ ! -f "$OUT_DIR/test.pcap" ]; then
    # Hand-craft a minimal PCAP file (global header + one dummy packet)
    python3 -c "
import struct, sys
# PCAP global header
sys.stdout.buffer.write(struct.pack('<I', 0xa1b2c3d4))  # magic
sys.stdout.buffer.write(struct.pack('<HH', 2, 4))       # version 2.4
sys.stdout.buffer.write(struct.pack('<i', 0))            # thiszone
sys.stdout.buffer.write(struct.pack('<I', 0))            # sigfigs
sys.stdout.buffer.write(struct.pack('<I', 65535))        # snaplen
sys.stdout.buffer.write(struct.pack('<I', 1))            # network (Ethernet)
# One packet record
packet = bytes(64)  # 64 bytes of zeros (dummy Ethernet frame)
sys.stdout.buffer.write(struct.pack('<I', 1000000))      # ts_sec
sys.stdout.buffer.write(struct.pack('<I', 0))            # ts_usec
sys.stdout.buffer.write(struct.pack('<I', len(packet)))  # incl_len
sys.stdout.buffer.write(struct.pack('<I', len(packet)))  # orig_len
sys.stdout.buffer.write(packet)
" > "$OUT_DIR/test.pcap"
    log_ok "PCAP (hand-crafted)"
else
    log_skip "PCAP" "already exists"
fi

echo ""
echo "=== Summary ==="
echo "Generated: $generated"
echo "Skipped:   $skipped"
echo "Failed:    $failed"
echo ""
echo "Files that could not be generated by this script will be"
echo "auto-generated by RealFileFixture using TestDataGenerator."
echo ""
echo "Formats needing TestDataGenerator fallback:"
echo "  GIF, WAV, MP3, FLAC, AVI, FLV, MIDI, WebP, ICO,"
echo "  ELF, PE, WASM, DNS, 7z, Parquet"
