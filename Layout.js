import React from 'react';
import Navigation from '@/components/Navigation';
import Footer from '@/components/Footer';

export default function Layout({ children }) {
  return (
    <div className="min-h-screen flex flex-col bg-white">
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap');
        
        * {
          font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
        }
        
        ::selection {
          background-color: #000;
          color: #fff;
        }
      `}</style>
      <Navigation />
      <main className="flex-1 pt-16">
        {children}
      </main>
      <Footer />
    </div>
  );
}