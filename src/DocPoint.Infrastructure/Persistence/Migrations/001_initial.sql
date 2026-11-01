CREATE TABLE folders (
    id                 INTEGER PRIMARY KEY,
    path               TEXT NOT NULL UNIQUE,
    include_subfolders INTEGER NOT NULL DEFAULT 1,
    ignore_patterns    TEXT NOT NULL DEFAULT '',
    auto_index         INTEGER NOT NULL DEFAULT 1,
    last_scan_at       TEXT,
    status             TEXT NOT NULL DEFAULT ''
);

CREATE TABLE documents (
    id          INTEGER PRIMARY KEY,
    folder_id   INTEGER NOT NULL REFERENCES folders(id) ON DELETE CASCADE,
    path        TEXT NOT NULL UNIQUE,
    file_name   TEXT NOT NULL,
    ext         TEXT NOT NULL,
    modified_at TEXT NOT NULL,
    size        INTEGER NOT NULL,
    hash        TEXT NOT NULL DEFAULT '',
    status      INTEGER NOT NULL DEFAULT 0,
    error       TEXT,
    indexed_at  TEXT
);

CREATE INDEX ix_documents_folder ON documents(folder_id);
CREATE INDEX ix_documents_name ON documents(file_name COLLATE NOCASE);
CREATE INDEX ix_documents_modified ON documents(modified_at);
CREATE INDEX ix_documents_ext ON documents(ext);

CREATE TABLE nodes (
    id           INTEGER PRIMARY KEY,
    document_id  INTEGER NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    parent_id    INTEGER REFERENCES nodes(id) ON DELETE CASCADE,
    kind         INTEGER NOT NULL,
    ordinal      INTEGER NOT NULL,
    number       TEXT NOT NULL DEFAULT '',
    title        TEXT NOT NULL DEFAULT '',
    content      TEXT NOT NULL DEFAULT '',
    label        TEXT NOT NULL DEFAULT '',
    page         INTEGER,
    confidence   REAL NOT NULL DEFAULT 1,
    needs_review INTEGER NOT NULL DEFAULT 0,
    review_reason TEXT
);

CREATE INDEX ix_nodes_document ON nodes(document_id, ordinal);
CREATE INDEX ix_nodes_parent ON nodes(parent_id);

-- Bảng tìm kiếm riêng, rowid = nodes.id. content_plain là bản không dấu (đ thành d) cùng độ dài với content.
CREATE VIRTUAL TABLE nodes_fts USING fts5(
    file_name, title, content, content_plain,
    tokenize = 'unicode61 remove_diacritics 2'
);

-- Xoá node (kể cả xoá dây chuyền khi xoá tài liệu) thì xoá luôn dòng tìm kiếm tương ứng.
CREATE TRIGGER nodes_fts_delete AFTER DELETE ON nodes
BEGIN
    DELETE FROM nodes_fts WHERE rowid = old.id;
END;
