interface HomePageProps {
  pageTitle: string;
}

import PageTitle from './PageTitle';

function HomePage({ pageTitle }: HomePageProps) {
  return (
    <>
      <PageTitle title={pageTitle} />
      <main className="app-main">
        <section className="app-page" aria-labelledby="home-heading">
          <h1 id="home-heading">{pageTitle}</h1>
          <p>Widgets coming soon</p>
        </section>
      </main>
    </>
  );
}

export default HomePage;
