"use client";

import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import api from "@/src/lib/axios";
import Link from "next/link";
import { useState } from "react";
import { toast } from "react-hot-toast";

type NoteDto = {
  id: number;
  title: string;
  description: string;
  filePath?: string | null;
  tags?: string | null;
  category?: string | null;
  createdAt: string;
};

type QuizQuestionDto = {
  question: string;
  options: string[];
  correctAnswer: string;
};

async function fetchNote(id: number): Promise<NoteDto> {
  const { data } = await api.get<NoteDto>(`/Notes/${id}`);
  return data;
}

async function fetchSummary(id: number): Promise<string> {
  const { data } = await api.get<{ summary: string }>(`/AI/${id}/summary`);
  return data.summary;
}

async function fetchQuiz(id: number): Promise<QuizQuestionDto[]> {
  const { data } = await api.get<QuizQuestionDto[]>(`/AI/${id}/quiz`);
  return data;
}

export default function NoteDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = Number(params.id);

  const [summary, setSummary] = useState<string | null>(null);
  const [summaryOpen, setSummaryOpen] = useState(false);

  const [quiz, setQuiz] = useState<QuizQuestionDto[] | null>(null);
  const [quizIndex, setQuizIndex] = useState(0);
  const [selected, setSelected] = useState<string | null>(null);
  const [answered, setAnswered] = useState(false);
  const [score, setScore] = useState(0);

  const assetBaseUrl =
    process.env.NEXT_PUBLIC_ASSET_BASE_URL ?? "http://localhost:5047";

  const {
    data: note,
    isLoading,
    error,
  } = useQuery({
    queryKey: ["note-detail", id],
    queryFn: () => fetchNote(id),
    enabled: Number.isFinite(id),
  });

  const summaryMutation = useMutation<string, unknown>({
    mutationFn: () => fetchSummary(id),
    onSuccess: (text) => {
      setSummary(text);
      setSummaryOpen(true);
      toast.success("Özet hazır.");
    },
    onError: (err) => {
      const msg =
        err?.response?.data?.message ??
        err?.message ??
        "Özet alınırken bir hata oluştu.";
      toast.error(msg);
    },
  });

  const quizMutation = useMutation<QuizQuestionDto[], unknown>({
    mutationFn: () => fetchQuiz(id),
    onSuccess: (questions) => {
      setQuiz(questions);
      setQuizIndex(0);
      setSelected(null);
      setAnswered(false);
      setScore(0);
      toast.success("Quiz hazır.");
    },
    onError: (err) => {
      const msg =
        err?.response?.data?.message ??
        err?.message ??
        "Quiz oluşturulurken bir hata oluştu.";
      toast.error(msg);
    },
  });

  const handleOptionClick = (option: string) => {
    if (!quiz || answered) return;
    setSelected(option);
    setAnswered(true);
    const current = quiz[quizIndex];
    if (option === current.correctAnswer) {
      setScore((s) => s + 1);
      toast.success("Doğru cevap!");
    } else {
      toast.error("Yanlış cevap.");
    }
  };

  const handleNextQuestion = () => {
    if (!quiz) return;
    if (quizIndex < quiz.length - 1) {
      setQuizIndex((i) => i + 1);
      setSelected(null);
      setAnswered(false);
    }
  };

  if (!Number.isFinite(id)) {
    return <div className="p-4 text-slate-200">Geçersiz not kimliği.</div>;
  }

  if (isLoading) {
    return <div className="p-4 text-slate-200">Yükleniyor...</div>;
  }

  if (error || !note) {
    return (
      <div className="p-4 text-slate-200">
        Not bulunamadı veya yüklenirken hata oluştu.
      </div>
    );
  }

  const tags =
    note.tags
      ?.split(",")
      .map((t) => t.trim())
      .filter((t) => t.length > 0) ?? [];

  return (
    <div className="min-h-screen bg-slate-950 text-slate-50">
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur sticky top-0 z-10">
        <div className="mx-auto max-w-4xl flex items-center justify-between px-4 py-3">
          <div>
            <h1 className="text-lg font-semibold tracking-tight">
              Not Detayı
            </h1>
            <p className="text-[11px] text-slate-500 mt-1">
              AI destekli özetleme ve quiz ile konuyu pekiştir.
            </p>
          </div>
          <button
            onClick={() => router.push("/dashboard")}
            className="text-xs px-3 py-1.5 rounded-full border border-slate-700 hover:bg-slate-800 transition"
          >
            ← Dashboard&apos;a Dön
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-6 space-y-6">
        <section className="rounded-2xl border border-slate-800 bg-slate-900/60 p-4 shadow-xl shadow-slate-950/40 space-y-2">
          <h2 className="text-sm font-semibold text-slate-100">
            {note.title}
          </h2>
          <p className="text-xs text-slate-300 whitespace-pre-line">
            {note.description}
          </p>
          <div className="mt-2 flex flex-wrap gap-1">
            {note.category && (
              <span className="rounded-full bg-indigo-500/15 border border-indigo-500/40 px-2 py-0.5 text-[10px] uppercase tracking-wide text-indigo-200">
                {note.category}
              </span>
            )}
            {tags.map((tag) => (
              <span
                key={tag}
                className="rounded-full bg-slate-800/80 px-2 py-0.5 text-[10px] text-slate-200 border border-slate-600/70"
              >
                {tag}
              </span>
            ))}
          </div>
          <p className="text-[11px] text-slate-500">
            Oluşturulma:{" "}
            {new Date(note.createdAt).toLocaleString("tr-TR")}
          </p>
          {note.filePath && (
            <div className="mt-2">
              <Link
                href={`${assetBaseUrl}/${note.filePath}`}
                target="_blank"
                className="inline-flex items-center rounded-full border border-emerald-500/60 px-3 py-1 text-[11px] text-emerald-300 hover:bg-emerald-500/10 transition"
              >
                Dosyayı Görüntüle
              </Link>
            </div>
          )}
        </section>

        <section className="rounded-2xl border border-slate-800 bg-slate-900/70 p-4 shadow-xl shadow-slate-950/40 space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-sm font-medium text-slate-100">
                AI Araçları
              </h2>
              <p className="text-[11px] text-slate-500 mt-1">
                Notunu hızlıca özetle veya kendini test et.
              </p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2 text-[11px]">
            <button
              onClick={() => summaryMutation.mutate()}
              disabled={summaryMutation.isPending}
              className="inline-flex items-center gap-2 rounded-xl border border-violet-500/60 bg-violet-950/40 px-4 py-2 text-xs font-medium text-violet-200 hover:bg-violet-500/20 disabled:opacity-60 transition"
            >
              {summaryMutation.isPending && (
                <span className="h-3 w-3 animate-spin rounded-full border-2 border-violet-200 border-t-transparent" />
              )}
              Özetle
            </button>
            <button
              onClick={() => quizMutation.mutate()}
              disabled={quizMutation.isPending}
              className="inline-flex items-center gap-2 rounded-xl border border-emerald-500/60 bg-emerald-950/40 px-4 py-2 text-xs font-medium text-emerald-200 hover:bg-emerald-500/20 disabled:opacity-60 transition"
            >
              {quizMutation.isPending && (
                <span className="h-3 w-3 animate-spin rounded-full border-2 border-emerald-200 border-t-transparent" />
              )}
              Beni Sınav Yap
            </button>
          </div>

          {summaryOpen && summary && (
            <div className="mt-2 rounded-xl border border-violet-700/60 bg-violet-950/40 p-3 text-xs text-violet-50 space-y-2">
              <div className="flex items-center justify-between">
                <h3 className="text-[12px] font-semibold">
                  AI Özeti (5 madde)
                </h3>
                <button
                  onClick={() => setSummaryOpen(false)}
                  className="text-[10px] px-2 py-0.5 rounded-full border border-violet-500/60 hover:bg-violet-500/20"
                >
                  Kapat
                </button>
              </div>
              <div className="mt-1 space-y-1">
                {summary.split("\n").map((line, idx) => (
                  <p key={idx} className="leading-relaxed">
                    {line}
                  </p>
                ))}
              </div>
            </div>
          )}

          {quiz && quiz.length > 0 && (
            <div className="mt-2 rounded-xl border border-emerald-700/60 bg-emerald-950/40 p-3 text-xs text-emerald-50 space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="text-[12px] font-semibold">
                  Quiz ({quizIndex + 1}/{quiz.length})
                </h3>
                <span className="text-[11px]">
                  Skor: {score}/{quiz.length}
                </span>
              </div>
              <p className="text-[13px] font-medium">
                {quiz[quizIndex].question}
              </p>
              <div className="mt-2 grid gap-2">
                {quiz[quizIndex].options.map((opt) => {
                  const isSelected = selected === opt;
                  const isCorrect =
                    answered && opt === quiz[quizIndex].correctAnswer;
                  const isWrong =
                    answered &&
                    isSelected &&
                    opt !== quiz[quizIndex].correctAnswer;
                  return (
                    <button
                      key={opt}
                      type="button"
                      disabled={answered}
                      onClick={() => handleOptionClick(opt)}
                      className={[
                        "w-full rounded-lg border px-3 py-2 text-left text-[12px] transition",
                        "border-emerald-500/50 bg-emerald-900/40 hover:bg-emerald-600/30",
                        isSelected ? "ring-1 ring-emerald-400" : "",
                        isCorrect
                          ? "border-emerald-400 bg-emerald-700/60"
                          : "",
                        isWrong ? "border-red-400 bg-red-800/60" : "",
                      ].join(" ")}
                    >
                      {opt}
                    </button>
                  );
                })}
              </div>
              <div className="flex flex-col gap-1 mt-2 text-[11px]">
                <div className="flex justify-between items-center">
                  <span>
                    {answered
                      ? "Sonucu ve doğru cevabı aşağıda görebilirsiniz."
                      : "Bir şık seçerek cevaplayın."}
                  </span>
                  {quizIndex < quiz.length - 1 ? (
                    <button
                      type="button"
                      disabled={!answered}
                      onClick={handleNextQuestion}
                      className="rounded-full border border-emerald-500/60 px-3 py-1 text-[11px] hover:bg-emerald-500/20 disabled:opacity-50"
                    >
                      Sonraki Soru →
                    </button>
                  ) : (
                    <div className="flex items-center gap-2">
                      <span className="font-semibold">
                        Quiz bitti, skorunuz: {score}/{quiz.length}
                      </span>
                      <button
                        type="button"
                        onClick={() => quizMutation.mutate()}
                        className="rounded-full border border-emerald-500/60 px-3 py-1 text-[11px] hover:bg-emerald-500/20"
                      >
                        Yeniden Çöz
                      </button>
                    </div>
                  )}
                </div>
                {answered && (
                  <span className="text-emerald-200">
                    Doğru cevap:{" "}
                    <span className="font-semibold">
                      {quiz[quizIndex].correctAnswer}
                    </span>
                  </span>
                )}
              </div>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

