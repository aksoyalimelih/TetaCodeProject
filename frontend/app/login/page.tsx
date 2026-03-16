"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import api from "@/src/lib/axios";
import Link from "next/link";
import { toast } from "react-hot-toast";

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const response = await api.post("/Auth/login", {
        email,
        password,
      });

      const token = response.data.token as string;
      localStorage.setItem("token", token);

      toast.success("Giriş başarılı, yönlendiriliyorsunuz.");
      router.push("/dashboard");
    } catch {
      setError("Giriş yapılamadı. E-posta veya şifre hatalı olabilir.");
      toast.error("Giriş başarısız. Bilgilerinizi kontrol edin.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-950 to-slate-900 px-4">
      <div className="w-full max-w-md rounded-3xl bg-slate-950/70 border border-slate-800 shadow-2xl shadow-slate-900/60 backdrop-blur-xl p-8 space-y-6">
        <div className="space-y-1 text-center">
          <h1 className="text-2xl font-semibold text-slate-50 tracking-tight">
            TetaCode Not Sistemi
          </h1>
          <p className="text-sm text-slate-400">
            Lütfen e-posta ve şifrenizle giriş yapın.
          </p>
        </div>

        <form className="space-y-4" onSubmit={handleSubmit}>
          <div className="space-y-2">
            <label className="block text-sm font-medium text-slate-200">
              E-posta
            </label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-xl border border-slate-700 bg-slate-900/60 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/40 transition"
              placeholder="ornek@eposta.com"
            />
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-medium text-slate-200">
              Şifre
            </label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-xl border border-slate-700 bg-slate-900/60 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/40 transition"
              placeholder="********"
            />
          </div>

          {error && (
            <p className="text-sm text-red-400 bg-red-950/40 border border-red-900 rounded-xl px-3 py-2">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full inline-flex items-center justify-center rounded-xl bg-indigo-500 hover:bg-indigo-400 disabled:bg-indigo-600/50 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/40 transition"
          >
            {loading ? "Giriş yapılıyor..." : "Giriş Yap"}
          </button>
        </form>

        <p className="text-xs text-slate-400 text-center">
          Hesabınız yok mu?{" "}
          <Link
            href="/register"
            className="text-indigo-400 hover:text-indigo-300 underline underline-offset-2"
          >
            Kayıt Ol
          </Link>
        </p>
      </div>
    </div>
  );
}

