"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { FormEvent, useEffect, useMemo, useState } from "react";
import api from "@/src/lib/axios";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { toast } from "react-hot-toast";
import dynamic from "next/dynamic";

const VoiceRecorder = dynamic(
  () => import("@/src/components/VoiceRecorder"),
  { ssr: false },
);
const AudioUpload = dynamic(
  () => import("./AudioUpload"),
  { ssr: false },
);

type NoteDto = {
  id: number;
  title: string;
  description: string;
  filePath?: string | null;
  tags?: string | null;
  category?: string | null;
  createdAt: string;
};

async function fetchNotes(search?: string, category?: string): Promise<NoteDto[]> {
  const params: Record<string, string> = {};
  if (search && search.trim().length > 0) {
    params.search = search.trim();
  }
  if (category && category !== "Hepsi") {
    params.category = category;
  }

  const response = await api.get<NoteDto[]>("/Notes", { params });
  return response.data;
}

export default function DashboardPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [editing, setEditing] = useState<NoteDto | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editFile, setEditFile] = useState<File | null>(null);
  const [category, setCategory] = useState("");
  const [editCategory, setEditCategory] = useState("");

  const [ocrOpen, setOcrOpen] = useState(false);
  const [ocrStep, setOcrStep] = useState<1 | 2 | 3>(1);
  const [ocrImage, setOcrImage] = useState<File | null>(null);
  const [ocrText, setOcrText] = useState("");
  const [ocrPdfTitle, setOcrPdfTitle] = useState("");
  const [ocrPdfDescription, setOcrPdfDescription] = useState("");
  const [ocrLanguage, setOcrLanguage] = useState<"eng" | "tur" | "eng+tur">("eng+tur");

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState<"Hepsi" | "Vize" | "Final" | "Genel">("Hepsi");

  const [audioModalOpen, setAudioModalOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [isCombining, setIsCombining] = useState(false);
  const [expandedIds, setExpandedIds] = useState<number[]>([]);
  const [editDescriptionFullOpen, setEditDescriptionFullOpen] = useState(false);
  const [editDescriptionFullTemp, setEditDescriptionFullTemp] = useState("");

  const assetBaseUrl =
    process.env.NEXT_PUBLIC_ASSET_BASE_URL ?? "http://localhost:5047";

  const handleDownload = async (id: number, type: "pdf" | "docx") => {
    try {
      const response = await api.get(
        `/Notes/${id}/download-${type}`,
        { responseType: "blob" },
      );
      const blob = new Blob([response.data]);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = type === "pdf" ? "note.pdf" : "note.docx";
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      const anyErr = err as { response?: { data?: { message?: string } } };
      const msg =
        anyErr?.response?.data?.message ??
        (err as Error).message ??
        "Dosya indirilirken bir hata oluştu.";
      toast.error(msg);
    }
  };

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
    }
  }, [router]);

  // basit debounce: kullanıcı yazmayı bıraktıktan 400ms sonra arama terimini uygula
  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedSearch(search);
    }, 400);
    return () => clearTimeout(handle);
  }, [search]);

  const { data: notes, isLoading } = useQuery({
    queryKey: ["notes", debouncedSearch, categoryFilter],
    queryFn: () => fetchNotes(debouncedSearch, categoryFilter === "Hepsi" ? undefined : categoryFilter),
  });

  const availableCategories = useMemo(() => {
    if (!notes) return ["Vize", "Final", "Genel"];
    const set = new Set<string>();
    notes.forEach((n) => {
      if (n.category && n.category.trim().length > 0) {
        set.add(n.category.trim());
      }
    });
    const base = ["Vize", "Final", "Genel"];
    const dynamic = Array.from(set).filter((c) => !base.includes(c));
    return [...base, ...dynamic].sort((a, b) => a.localeCompare(b, "tr-TR"));
  }, [notes]);

  const createNoteMutation = useMutation({
    mutationFn: async () => {
      const formData = new FormData();
      formData.append("Title", title);
      formData.append("Description", description);
      if (category.trim().length > 0) {
        formData.append("Category", category.trim());
      }
      if (file) {
        formData.append("File", file);
      }
      await api.post("/Notes", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
    },
    onSuccess: () => {
      setTitle("");
      setDescription("");
      setFile(null);
       setCategory("");
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      toast.success("Not başarıyla eklendi.");
    },
    onError: () => {
      setError("Not eklenirken bir hata oluştu.");
      toast.error("Not eklenirken hata oluştu.");
    },
  });

  const archiveMutation = useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`/Notes/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      queryClient.invalidateQueries({ queryKey: ["notes-archive"] });
      toast.success("Not arşive taşındı.");
    },
    onError: () => {
      toast.error("Not arşive taşınamadı.");
    },
  });

  const updateNoteMutation = useMutation({
    mutationFn: async () => {
      if (!editing) return;

      const formData = new FormData();
      formData.append("Title", editTitle);
      formData.append("Description", editDescription);
      if (editCategory.trim().length > 0) {
        formData.append("Category", editCategory.trim());
      }
      if (editFile) {
        formData.append("File", editFile);
      }

      await api.put(`/Notes/${editing.id}`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
    },
    onSuccess: () => {
      setEditOpen(false);
      setEditing(null);
      setEditFile(null);
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      toast.success("Not başarıyla güncellendi.");
    },
    onError: () => {
      toast.error("Not güncellenirken hata oluştu.");
    },
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    createNoteMutation.mutate();
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    router.push("/login");
  };

  const openEdit = (note: NoteDto) => {
    setEditing(note);
    setEditTitle(note.title);
    setEditDescription(note.description);
    setEditCategory(note.category ?? "");
    setEditFile(null);
    setEditOpen(true);
  };

  const analyzeImageMutation = useMutation({
    mutationFn: async ({ file: imageFile, language }: { file: File; language: string }) => {
      const formData = new FormData();
      formData.append("file", imageFile);
      formData.append("language", language);
      const { data } = await api.post<{ text: string }>("/Notes/analyze-image", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      return data.text;
    },
    onSuccess: (text) => {
      setOcrText(text ?? "");
      setOcrStep(2);
      toast.success("Metin başarıyla okundu. Gerekirse düzenleyip PDF olarak kaydedebilirsiniz.");
    },
    onError: (err: { response?: { data?: string } }) => {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? err?.response?.data ?? "Görsel analiz edilirken hata oluştu.";
      toast.error(typeof msg === "string" ? msg : "Görsel analiz edilirken hata oluştu.");
    },
  });

  const convertToPdfMutation = useMutation({
    mutationFn: async (payload: { title: string; description: string; content: string }) => {
      const { data } = await api.post<NoteDto>("/Notes/convert-to-pdf", payload, {
        headers: { "Content-Type": "application/json" },
      });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      setOcrOpen(false);
      setOcrStep(1);
      setOcrImage(null);
      setOcrText("");
      setOcrPdfTitle("");
      setOcrPdfDescription("");
      toast.success("PDF notu başarıyla oluşturuldu.");
    },
    onError: () => {
      toast.error("PDF oluşturulurken hata oluştu.");
    },
  });

  const openOcrModal = () => {
    setOcrStep(1);
    setOcrImage(null);
    setOcrText("");
    setOcrPdfTitle("");
    setOcrPdfDescription("");
    setOcrLanguage("eng+tur");
    setOcrOpen(true);
  };

  const closeOcrModal = () => {
    setOcrOpen(false);
    setOcrStep(1);
    setOcrImage(null);
    setOcrText("");
    setOcrPdfTitle("");
    setOcrPdfDescription("");
  };

  const handleOcrAnalyze = () => {
    if (!ocrImage) {
      toast.error("Lütfen bir görsel seçin.");
      return;
    }
    analyzeImageMutation.mutate({ file: ocrImage, language: ocrLanguage });
  };

  const handleOcrSavePdf = () => {
    const title = ocrPdfTitle.trim() || "OCR Belge";
    convertToPdfMutation.mutate({
      title,
      description: ocrPdfDescription.trim(),
      content: ocrText,
    });
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-50">
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur sticky top-0 z-10">
        <div className="mx-auto max-w-5xl flex items-center justify-between px-4 py-3">
          <div>
            <h1 className="text-lg font-semibold tracking-tight">
              Notlarım Dashboard
            </h1>
            <div className="mt-2 inline-flex rounded-full bg-slate-900/80 p-1 text-xs">
              <Link
                href="/dashboard"
                className="px-4 py-1.5 rounded-full bg-slate-800 text-slate-50"
              >
                Notlarım
              </Link>
              <Link
                href="/archive"
                className="px-4 py-1.5 rounded-full text-slate-400 hover:text-slate-100 hover:bg-slate-800 transition"
              >
                Arşivim
              </Link>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="text-xs px-3 py-1.5 rounded-full border border-slate-700 hover:bg-slate-800 transition"
          >
            Çıkış Yap
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6 space-y-6">
        <datalist id="categoryOptions">
          {availableCategories.map((cat) => (
            <option key={cat} value={cat} />
          ))}
        </datalist>
        <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-4 shadow-xl shadow-slate-950/40 space-y-4">
          <h2 className="text-base font-semibold text-slate-100 mb-1">
            Yeni Not Ekle
          </h2>
          <p className="text-[11px] text-slate-500">
            Ders başlığı, kısa açıklama, isteğe bağlı dosya ve kategori bilgisiyle yeni bir not oluştur.
          </p>
          <form
            onSubmit={handleSubmit}
            className="grid gap-3 md:grid-cols-[2fr,3fr,2fr,2fr,auto]"
          >
            <div className="space-y-1">
              <label className="block text-[11px] text-slate-400">Ders Adı</label>
              <input
                type="text"
                required
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Örn. Mobil Programlama"
                className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-[11px] text-slate-400">Kısa Açıklama</label>
              <input
                type="text"
                required
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Ders içeriği hakkında kısa not"
                className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-[11px] text-slate-400">Dosya (opsiyonel)</label>
              <input
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                className="w-full text-xs file:mr-2 file:rounded-full file:border-0 file:bg-indigo-500 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-white hover:file:bg-indigo-400"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-[11px] text-slate-400">Kategori</label>
              <input
                type="text"
                list="categoryOptions"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                placeholder="Örn. Vize, Final..."
                className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
              />
            </div>
            <button
              type="submit"
              disabled={createNoteMutation.isPending}
              className="inline-flex items-center justify-center rounded-xl bg-indigo-500 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/40 hover:bg-indigo-400 disabled:bg-indigo-600/50"
            >
              {createNoteMutation.isPending ? "Ekleniyor..." : "Ekle"}
            </button>
          </form>
          {error && (
            <p className="mt-2 text-xs text-red-400 bg-red-950/40 border border-red-900 rounded-xl px-3 py-1.5">
              {error}
            </p>
          )}
          <div className="mt-3 pt-3 border-t border-slate-800 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={openOcrModal}
              className="inline-flex items-center gap-2 rounded-xl border border-violet-500/60 bg-violet-950/40 px-4 py-2 text-xs font-medium text-violet-200 hover:bg-violet-500/20 transition"
            >
              <span aria-hidden>📄</span>
              Görselden Metne (OCR)
            </button>
            <button
              type="button"
              onClick={() => setAudioModalOpen(true)}
              className="inline-flex items-center gap-2 rounded-xl border border-emerald-500/60 bg-emerald-950/40 px-4 py-2 text-xs font-medium text-emerald-200 hover:bg-emerald-500/20 transition"
            >
              <span aria-hidden>🎤</span>
              Yeni Sesli Not
            </button>
          </div>
        </section>

        <section className="space-y-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-sm font-medium text-slate-100">Aktif Notlar</h2>
              <p className="text-[11px] text-slate-500 mt-1">
                Başlık, açıklama veya etiketler içinde arama yapabilirsiniz. Birden fazla notu
                seçip yapay zeka ile birleştirebilirsiniz.
              </p>
            </div>
            <div className="flex flex-col gap-2 md:flex-row md:items-center">
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Ara: Örn. 'nesne yönelimli', 'pdf', 'matematik'"
                className="w-full md:w-64 rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
              />
              <select
                value={categoryFilter}
                onChange={(e) =>
                  setCategoryFilter(
                    e.target.value as "Hepsi" | "Vize" | "Final" | "Genel",
                  )
                }
                className="w-full md:w-40 rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs text-slate-100 outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
              >
                <option value="Hepsi">Tüm Kategoriler</option>
                {availableCategories.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>
            </div>
          </div>
          {selectedIds.length >= 2 && (
            <div className="flex items-center justify-between rounded-xl border border-sky-700/60 bg-sky-950/40 px-3 py-2 text-[11px] text-sky-50">
              <span>
                {selectedIds.length} not seçildi. Bunları yapay zeka ile birleştirebilirsiniz.
              </span>
              <button
                type="button"
                disabled={isCombining}
                onClick={async () => {
                  try {
                    setIsCombining(true);
                    const { data } = await api.post<NoteDto>("/Synthesis/combine", {
                      noteIds: selectedIds,
                    });
                    setSelectedIds([]);
                    await queryClient.invalidateQueries({ queryKey: ["notes"] });
                    toast.success("Birleştirilmiş not oluşturuldu.");
                    router.push(`/note/${data.id}`);
                  } catch (err) {
                    const msg =
                      err?.response?.data?.message ??
                      err?.message ??
                      "Notlar birleştirilirken bir hata oluştu.";
                    toast.error(msg);
                  } finally {
                    setIsCombining(false);
                  }
                }}
                className="inline-flex items-center gap-2 rounded-full border border-sky-400/80 bg-sky-500/20 px-3 py-1 text-[11px] font-medium text-sky-100 hover:bg-sky-400/30 disabled:opacity-60"
              >
                {isCombining && (
                  <span className="h-3 w-3 animate-spin rounded-full border-2 border-sky-200 border-t-transparent" />
                )}
                ✨ Seçilenleri Yapay Zeka ile Birleştir
              </button>
            </div>
          )}

          {isLoading ? (
            <p className="text-xs text-slate-400">Yükleniyor...</p>
          ) : !notes || notes.length === 0 ? (
            <p className="text-xs text-slate-500">
              Henüz hiç not eklenmemiş. Yukarıdaki formdan bir not ekleyebilirsin.
            </p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              {notes.map((note) => {
                const isSelected = selectedIds.includes(note.id);
                return (
                  <article
                    key={note.id}
                    className={`flex flex-col justify-between rounded-2xl border p-4 shadow-lg shadow-slate-950/40 transition-colors ${
                      isSelected
                        ? "border-sky-500 bg-sky-950/40"
                        : "border-slate-800 bg-slate-900/70"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="space-y-1">
                      <h3 className="text-sm font-semibold text-slate-50">
                        {note.title}
                      </h3>
                      <div className="relative text-xs text-slate-300">
                        {(() => {
                          const full = note.description ?? "";
                          const isExpanded = expandedIds.includes(note.id);
                          const isLong = full.length > 300;
                          const visibleText =
                            !isLong || isExpanded
                              ? full
                              : `${full.slice(0, 300)}...`;

                          return (
                            <>
                              <p className="whitespace-pre-line transition-all duration-200">
                                {visibleText}
                              </p>
                              {!isExpanded && isLong && (
                                <div className="pointer-events-none absolute inset-x-0 bottom-0 h-6 bg-gradient-to-t from-slate-900 to-transparent" />
                              )}
                              {isLong && (
                                <button
                                  type="button"
                                  onClick={() =>
                                    setExpandedIds((prev) =>
                                      prev.includes(note.id)
                                        ? prev.filter((x) => x !== note.id)
                                        : [...prev, note.id],
                                    )
                                  }
                                  className="mt-1 text-[11px] text-sky-300 hover:text-sky-200 underline-offset-2 hover:underline"
                                >
                                  {isExpanded ? "Daha Az Göster" : "Daha Fazla Göster"}
                                </button>
                              )}
                            </>
                          );
                        })()}
                      </div>
                      <div className="mt-1 flex flex-wrap gap-1">
                        {note.category && (
                          <span className="rounded-full bg-indigo-500/15 border border-indigo-500/40 px-2 py-0.5 text-[10px] uppercase tracking-wide text-indigo-200">
                            {note.category}
                          </span>
                        )}
                        {note.tags &&
                          note.tags
                            .split(",")
                            .map((t) => t.trim())
                            .filter((t) => t.length > 0)
                            .map((tag) => (
                              <span
                                key={tag}
                                className="rounded-full bg-slate-800/80 px-2 py-0.5 text-[10px] text-slate-200 border border-slate-600/70"
                              >
                                {tag}
                              </span>
                            ))}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <input
                        type="checkbox"
                        checked={selectedIds.includes(note.id)}
                        onChange={(e) => {
                          setSelectedIds((prev) =>
                            e.target.checked
                              ? [...prev, note.id]
                              : prev.filter((x) => x !== note.id),
                          );
                        }}
                        className="h-4 w-4 rounded border-slate-600 bg-slate-900 text-sky-400 focus:ring-sky-500"
                        aria-label="Notu birleştirmeye dahil et"
                      />
                        <p className="text-[11px] text-slate-500">
                          {new Date(note.createdAt).toLocaleString("tr-TR")}
                        </p>
                      </div>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2 text-[11px] items-center">
                      <button
                        className="rounded-full border border-slate-700 px-3 py-1 hover:bg-slate-800 transition"
                        onClick={() => openEdit(note)}
                      >
                        Düzenle
                      </button>
                      <button
                        onClick={() => router.push(`/note/${note.id}`)}
                        className="rounded-full border border-sky-500/60 px-3 py-1 text-sky-300 hover:bg-sky-500/10 transition"
                      >
                        Detay
                      </button>
                      <button
                        onClick={() => archiveMutation.mutate(note.id)}
                        className="rounded-full border border-amber-500/50 px-3 py-1 text-amber-300 hover:bg-amber-500/10 transition"
                      >
                        Arşive Gönder
                      </button>
                      <div className="relative">
                        <details className="group">
                          <summary className="list-none rounded-full border border-slate-700 px-3 py-1 text-slate-200 hover:bg-slate-800 cursor-pointer inline-flex items-center gap-1">
                            Daha Fazla
                            <span className="transition-transform group-open:rotate-180">
                              ▾
                            </span>
                          </summary>
                          <div className="absolute right-0 mt-1 min-w-[160px] rounded-xl border border-slate-800 bg-slate-950 py-1 text-[11px] shadow-lg z-10">
                            <button
                              type="button"
                              onClick={() => handleDownload(note.id, "pdf")}
                              className="block w-full px-3 py-1 text-left text-emerald-300 hover:bg-slate-800"
                            >
                              PDF İndir
                            </button>
                            <button
                              type="button"
                              onClick={() => handleDownload(note.id, "docx")}
                              className="block w-full px-3 py-1 text-left text-indigo-300 hover:bg-slate-800"
                            >
                              Word İndir
                            </button>
                            {note.filePath && (
                              <a
                                href={`${assetBaseUrl}/${note.filePath}`}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="block w-full px-3 py-1 text-left text-emerald-300 hover:bg-slate-800"
                              >
                                Orijinal Dosyayı Aç
                              </a>
                            )}
                          </div>
                        </details>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>

        {ocrOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
            <div className="w-full max-w-2xl rounded-2xl border border-slate-800 bg-slate-950 p-5 shadow-2xl">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-semibold text-slate-100">
                  Akıllı Belge İşleme (OCR)
                </h3>
                <button
                  type="button"
                  onClick={closeOcrModal}
                  className="text-xs px-2 py-1 rounded-md border border-slate-700 hover:bg-slate-800 text-slate-300"
                >
                  Kapat
                </button>
              </div>

              <div className="flex gap-2 mb-4">
                <div
                  className={`flex-1 rounded-lg py-1.5 text-center text-xs font-medium ${
                    ocrStep >= 1 ? "bg-violet-500/20 text-violet-200" : "bg-slate-800/50 text-slate-500"
                  }`}
                >
                  1. Görsel seç
                </div>
                <div
                  className={`flex-1 rounded-lg py-1.5 text-center text-xs font-medium ${
                    ocrStep >= 2 ? "bg-violet-500/20 text-violet-200" : "bg-slate-800/50 text-slate-500"
                  }`}
                >
                  2. Metni düzenle
                </div>
                <div
                  className={`flex-1 rounded-lg py-1.5 text-center text-xs font-medium ${
                    ocrStep >= 3 ? "bg-violet-500/20 text-violet-200" : "bg-slate-800/50 text-slate-500"
                  }`}
                >
                  3. PDF kaydet
                </div>
              </div>

              {ocrStep === 1 && (
                <div className="space-y-3">
                  <p className="text-xs text-slate-400">
                    Metin okunacak bir görsel seçin (PNG, JPG vb.). Analiz birkaç saniye sürebilir.
                  </p>
                  <div>
                    <label className="block text-xs text-slate-400 mb-1">OCR dili</label>
                    <select
                      value={ocrLanguage}
                      onChange={(e) => setOcrLanguage(e.target.value as "eng" | "tur" | "eng+tur")}
                      className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs text-slate-100 outline-none focus:ring-2 focus:ring-violet-500/40 focus:border-violet-500"
                    >
                      <option value="eng+tur">Türkçe + İngilizce</option>
                      <option value="tur">Türkçe</option>
                      <option value="eng">İngilizce</option>
                    </select>
                  </div>
                  <input
                    type="file"
                    accept="image/*"
                    onChange={(e) => setOcrImage(e.target.files?.[0] ?? null)}
                    className="text-xs file:mr-2 file:rounded-full file:border-0 file:bg-violet-500 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-white hover:file:bg-violet-400"
                  />
                  {ocrImage && (
                    <p className="text-xs text-slate-500">
                      Seçilen: {ocrImage.name}
                    </p>
                  )}
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={closeOcrModal}
                      className="rounded-xl border border-slate-700 px-4 py-2 text-xs hover:bg-slate-800"
                    >
                      Vazgeç
                    </button>
                    <button
                      type="button"
                      onClick={handleOcrAnalyze}
                      disabled={!ocrImage || analyzeImageMutation.isPending}
                      className="rounded-xl bg-violet-500 px-4 py-2 text-xs font-semibold text-white hover:bg-violet-400 disabled:opacity-50"
                    >
                      {analyzeImageMutation.isPending ? (
                        <span className="inline-flex items-center gap-2">
                          <span className="h-3 w-3 animate-spin rounded-full border-2 border-white border-t-transparent" />
                          Metin analiz ediliyor...
                        </span>
                      ) : (
                        "Analiz Et"
                      )}
                    </button>
                  </div>
                </div>
              )}

              {ocrStep === 2 && (
                <div className="space-y-3">
                  <p className="text-xs text-slate-400">
                    Okunan metni aşağıda düzenleyebilirsiniz. Sonra &quot;PDF Olarak Kaydet&quot; adımına geçin.
                  </p>
                  <textarea
                    value={ocrText}
                    onChange={(e) => setOcrText(e.target.value)}
                    rows={12}
                    className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs text-slate-100 placeholder-slate-500 outline-none focus:ring-2 focus:ring-violet-500/40 focus:border-violet-500 resize-y"
                    placeholder="Okunan metin burada görünecek..."
                  />
                  <div className="flex justify-between">
                    <button
                      type="button"
                      onClick={() => setOcrStep(1)}
                      className="rounded-xl border border-slate-700 px-4 py-2 text-xs hover:bg-slate-800"
                    >
                      Geri
                    </button>
                    <button
                      type="button"
                      onClick={() => setOcrStep(3)}
                      className="rounded-xl bg-violet-500 px-4 py-2 text-xs font-semibold text-white hover:bg-violet-400"
                    >
                      İleri: PDF Olarak Kaydet
                    </button>
                  </div>
                </div>
              )}

              {ocrStep === 3 && (
                <div className="space-y-3">
                  <p className="text-xs text-slate-400">
                    Not başlığı ve isteğe bağlı açıklama girin; PDF notu oluşturulacak.
                  </p>
                  <input
                    type="text"
                    value={ocrPdfTitle}
                    onChange={(e) => setOcrPdfTitle(e.target.value)}
                    placeholder="Başlık (zorunlu)"
                    className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-violet-500/40 focus:border-violet-500"
                  />
                  <input
                    type="text"
                    value={ocrPdfDescription}
                    onChange={(e) => setOcrPdfDescription(e.target.value)}
                    placeholder="Açıklama (isteğe bağlı)"
                    className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-violet-500/40 focus:border-violet-500"
                  />
                  <div className="flex justify-between">
                    <button
                      type="button"
                      onClick={() => setOcrStep(2)}
                      className="rounded-xl border border-slate-700 px-4 py-2 text-xs hover:bg-slate-800"
                    >
                      Geri
                    </button>
                    <button
                      type="button"
                      onClick={handleOcrSavePdf}
                      disabled={convertToPdfMutation.isPending}
                      className="rounded-xl bg-violet-500 px-4 py-2 text-xs font-semibold text-white hover:bg-violet-400 disabled:opacity-50"
                    >
                      {convertToPdfMutation.isPending ? "Kaydediliyor..." : "PDF Olarak Kaydet"}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {audioModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
            <div className="w-full max-w-xl rounded-2xl border border-slate-800 bg-slate-950 p-5 shadow-2xl space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-100">
                  Yeni Sesli Not
                </h3>
                <button
                  type="button"
                  onClick={() => setAudioModalOpen(false)}
                  className="text-xs px-2 py-1 rounded-md border border-slate-700 hover:bg-slate-800 text-slate-300"
                >
                  Kapat
                </button>
              </div>

              <p className="text-[11px] text-slate-400">
                İstersen hazır bir ses dosyası yükleyebilir, istersen doğrudan
                mikrofon üzerinden canlı kayıt alabilirsin.
              </p>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <h4 className="text-xs font-semibold text-slate-100">
                    Ses Dosyası Yükle
                  </h4>
                  <AudioUpload
                    onSuccess={() => {
                      setAudioModalOpen(false);
                      queryClient.invalidateQueries({ queryKey: ["notes"] });
                    }}
                  />
                </div>
                <div className="space-y-2">
                  <h4 className="text-xs font-semibold text-slate-100">
                    Canlı Ses Kaydı
                  </h4>
                  <VoiceRecorder
                    onSuccess={() => {
                      queryClient.invalidateQueries({ queryKey: ["notes"] });
                    }}
                  />
                </div>
              </div>
            </div>
          </div>
        )}

        {editOpen && editing && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4">
            <div className="w-full max-w-lg rounded-2xl border border-slate-800 bg-slate-950 p-4 shadow-2xl">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold">Notu Düzenle</h3>
                <button
                  onClick={() => setEditOpen(false)}
                  className="text-xs px-2 py-1 rounded-md border border-slate-700 hover:bg-slate-800"
                >
                  Kapat
                </button>
              </div>

              <div className="mt-4 space-y-3">
                <input
                  type="text"
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
                  placeholder="Ders Adı"
                />
                <div className="flex items-center gap-2">
                  <input
                    type="text"
                    value={editDescription}
                    onChange={(e) => setEditDescription(e.target.value)}
                    className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
                    placeholder="Açıklama"
                  />
                  <button
                    type="button"
                    onClick={() => {
                      setEditDescriptionFullTemp(editDescription);
                      setEditDescriptionFullOpen(true);
                    }}
                    className="shrink-0 rounded-xl border border-slate-600 bg-slate-800 px-3 py-2 text-[11px] text-slate-100 hover:bg-slate-700"
                    title="İçeriği büyük pencerede düzenle"
                  >
                    Büyüt
                  </button>
                </div>
                <input
                  type="text"
                  list="categoryOptions"
                  value={editCategory}
                  onChange={(e) => setEditCategory(e.target.value)}
                  className="w-full rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500"
                  placeholder="Kategori (örn. Vize, Final...)"
                />
                <input
                  type="file"
                  onChange={(e) => setEditFile(e.target.files?.[0] ?? null)}
                  className="text-xs file:mr-2 file:rounded-full file:border-0 file:bg-indigo-500 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-white hover:file:bg-indigo-400"
                />

                <div className="flex justify-end gap-2">
                  <button
                    onClick={() => setEditOpen(false)}
                    className="rounded-xl border border-slate-700 px-4 py-2 text-xs hover:bg-slate-800"
                  >
                    Vazgeç
                  </button>
                  <button
                    onClick={() => updateNoteMutation.mutate()}
                    disabled={updateNoteMutation.isPending}
                    className="rounded-xl bg-indigo-500 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/40 hover:bg-indigo-400 disabled:bg-indigo-600/50"
                  >
                    {updateNoteMutation.isPending ? "Kaydediliyor..." : "Kaydet"}
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {editDescriptionFullOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4">
            <div className="w-full max-w-3xl max-h-[90vh] rounded-2xl border border-slate-800 bg-slate-950 p-5 shadow-2xl flex flex-col animate-[fadeIn_0.15s_ease-out]">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-sm font-semibold text-slate-100">
                  İçeriği Düzenle (Geniş)
                </h3>
                <button
                  type="button"
                  onClick={() => setEditDescriptionFullOpen(false)}
                  className="text-xs px-2 py-1 rounded-md border border-slate-700 hover:bg-slate-800 text-slate-300"
                >
                  Kapat
                </button>
              </div>
              <div className="flex-1 overflow-auto mb-2">
                <textarea
                  value={editDescriptionFullTemp}
                  onChange={(e) => setEditDescriptionFullTemp(e.target.value)}
                  className="w-full h-[60vh] rounded-xl border border-slate-700 bg-slate-950/80 px-3 py-2 text-xs text-slate-100 outline-none focus:ring-2 focus:ring-indigo-500/40 focus:border-indigo-500 resize-none"
                  spellCheck={false}
                />
              </div>
              <div className="flex justify-between items-center mb-3 text-[10px] text-slate-500">
                <span>
                  {editDescriptionFullTemp.length.toLocaleString("tr-TR")} karakter
                </span>
              </div>
              <div className="flex justify-end gap-2 text-[11px]">
                <button
                  type="button"
                  onClick={() => setEditDescriptionFullOpen(false)}
                  className="rounded-xl border border-slate-700 px-4 py-2 hover:bg-slate-800 text-slate-200"
                >
                  Vazgeç
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setEditDescription(editDescriptionFullTemp);
                    setEditDescriptionFullOpen(false);
                  }}
                  className="rounded-xl bg-indigo-500 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/40 hover:bg-indigo-400"
                >
                  Değişiklikleri Uygula
                </button>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

