"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import api from "@/src/lib/axios";
import Link from "next/link";
import { toast } from "react-hot-toast";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";

type NoteDto = {
  id: number;
  title: string;
  description: string;
  filePath?: string | null;
  tags?: string | null;
  category?: string | null;
  createdAt: string;
};

async function fetchArchivedNotes(params: { search?: string; category?: string }): Promise<NoteDto[]> {
  const sp = new URLSearchParams();
  if (params.search?.trim()) sp.set("search", params.search.trim());
  if (params.category?.trim() && params.category !== "Hepsi") sp.set("category", params.category.trim());
  const q = sp.toString();
  const url = `/Notes/archive${q ? `?${q}` : ""}`;
  const response = await api.get<NoteDto[]>(url);
  return response.data;
}

export default function ArchivePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState<string>("Hepsi");
  const [expandedIds, setExpandedIds] = useState<number[]>([]);

  const assetBaseUrl =
    process.env.NEXT_PUBLIC_ASSET_BASE_URL ?? "http://localhost:5047";

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      router.push("/login");
    }
  }, [router]);

  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(t);
  }, [search]);

  const { data: notes, isLoading } = useQuery({
    queryKey: ["notes-archive", debouncedSearch, categoryFilter],
    queryFn: () => fetchArchivedNotes({ search: debouncedSearch, category: categoryFilter }),
  });

  const availableCategories = useMemo(() => {
    const base = ["Vize", "Final", "Genel"];
    if (!notes || notes.length === 0) return ["Hepsi", ...base];
    const set = new Set<string>();
    notes.forEach((n) => {
      if (n.category && n.category.trim().length > 0) set.add(n.category.trim());
    });
    const dynamic = Array.from(set).filter((c) => !base.includes(c));
    return ["Hepsi", ...base, ...dynamic].sort((a, b) => (a === "Hepsi" ? -1 : b === "Hepsi" ? 1 : a.localeCompare(b, "tr-TR")));
  }, [notes]);

  const restoreMutation = useMutation({
    mutationFn: async (id: number) => {
      await api.post(`/Notes/${id}/restore`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes"] });
      queryClient.invalidateQueries({ queryKey: ["notes-archive"] });
      toast.success("Not başarıyla geri yüklendi.");
    },
    onError: () => {
      toast.error("Not geri yüklenirken hata oluştu.");
    },
  });

  const hardDeleteMutation = useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`/Notes/${id}/hard`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notes-archive"] });
      toast.success("Not kalıcı olarak silindi.");
    },
    onError: () => {
      toast.error("Not silinirken hata oluştu.");
    },
  });

  const handleLogout = () => {
    localStorage.removeItem("token");
    router.push("/login");
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-50">
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur sticky top-0 z-10">
        <div className="mx-auto max-w-5xl flex items-center justify-between px-4 py-3">
          <div>
            <h1 className="text-lg font-semibold tracking-tight">
              Arşivlenmiş Notlar
            </h1>
            <div className="mt-2 inline-flex rounded-full bg-slate-900/80 p-1 text-xs">
              <Link
                href="/dashboard"
                className="px-4 py-1.5 rounded-full text-slate-400 hover:text-slate-100 hover:bg-slate-800 transition"
              >
                Notlarım
              </Link>
              <Link
                href="/archive"
                className="px-4 py-1.5 rounded-full bg-slate-800 text-slate-50"
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
        <section className="space-y-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-sm font-medium text-slate-100">
                Arşivlenmiş Notlar
              </h2>
              <p className="text-[11px] text-slate-500 mt-1">
                Başlık, açıklama veya etiketlere göre arayıp kategori ile filtreleyebilirsiniz.
              </p>
            </div>
            <div className="flex flex-col gap-2 md:flex-row md:items-center">
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Ara: başlık, açıklama veya etiket..."
                className="w-full md:w-64 rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs outline-none focus:ring-2 focus:ring-amber-500/40 focus:border-amber-500"
              />
              <select
                value={categoryFilter}
                onChange={(e) => setCategoryFilter(e.target.value)}
                className="w-full md:w-40 rounded-xl border border-slate-700 bg-slate-950/60 px-3 py-2 text-xs text-slate-100 outline-none focus:ring-2 focus:ring-amber-500/40 focus:border-amber-500"
              >
                {availableCategories.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat === "Hepsi" ? "Tüm Kategoriler" : cat}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {isLoading ? (
            <p className="text-xs text-slate-400">Yükleniyor...</p>
          ) : !notes || notes.length === 0 ? (
            <p className="text-xs text-slate-500">
              Arşivde hiç not yok. Dashboard üzerinden notları arşive
              gönderebilirsiniz.
            </p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              {notes.map((note) => {
                const full = note.description ?? "";
                const isExpanded = expandedIds.includes(note.id);
                const isLong = full.length > 300;
                const visibleText =
                  !isLong || isExpanded ? full : `${full.slice(0, 300)}...`;

                return (
                  <article
                    key={note.id}
                    className="flex flex-col justify-between rounded-2xl border border-slate-800 bg-slate-900/70 p-4 shadow-lg shadow-slate-950/40"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="space-y-1 flex-1 min-w-0">
                        <h3 className="text-sm font-semibold text-slate-50">
                          {note.title}
                        </h3>
                        <div className="relative text-xs text-slate-300">
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
                      <div className="flex flex-col items-end gap-1 shrink-0">
                        <p className="text-[11px] text-slate-500">
                          {new Date(note.createdAt).toLocaleString("tr-TR")}
                        </p>
                      </div>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2 text-[11px] items-center">
                      <button
                        onClick={() => restoreMutation.mutate(note.id)}
                        disabled={restoreMutation.isPending}
                        className="rounded-full border border-emerald-500/60 px-3 py-1 text-emerald-300 hover:bg-emerald-500/10 transition disabled:opacity-50"
                      >
                        Geri Yükle
                      </button>
                      <button
                        onClick={() => hardDeleteMutation.mutate(note.id)}
                        disabled={hardDeleteMutation.isPending}
                        className="rounded-full border border-red-500/60 px-3 py-1 text-red-300 hover:bg-red-500/10 transition disabled:opacity-50"
                      >
                        Kalıcı Sil
                      </button>
                      <button
                        type="button"
                        onClick={() => router.push(`/note/${note.id}`)}
                        className="rounded-full border border-sky-500/60 px-3 py-1 text-sky-300 hover:bg-sky-500/10 transition"
                      >
                        Detay
                      </button>
                      {note.filePath && (
                        <a
                          href={`${assetBaseUrl}/${note.filePath}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="rounded-full border border-slate-600 px-3 py-1 text-slate-200 hover:bg-slate-800 transition"
                        >
                          Dosyayı Görüntüle
                        </a>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
